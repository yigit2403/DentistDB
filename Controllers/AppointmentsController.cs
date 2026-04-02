using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AppointmentsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? status, int? patientId, AppointmentCalendarView view = AppointmentCalendarView.Daily, DateTime? date = null)
    {
        var query = _db.Appointments.Include(a => a.Patient).AsQueryable();
        var referenceDate = (date ?? DateTime.Today).Date;

        if (Enum.TryParse<AppointmentStatus>(status, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        if (patientId.HasValue)
        {
            query = query.Where(a => a.PatientId == patientId);
        }

        var (periodStart, periodEnd, previousDate, nextDate, buckets, periodLabel) = BuildCalendarFrame(view, referenceDate);
        var appointments = await query
            .Where(a => a.AppointmentDate >= periodStart && a.AppointmentDate < periodEnd)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();

        foreach (var appointment in appointments)
        {
            var bucket = buckets.FirstOrDefault(item =>
                appointment.AppointmentDate >= item.Start &&
                appointment.AppointmentDate < GetBucketEnd(item, view));

            bucket?.Appointments.Add(appointment);
        }

        var vm = new AppointmentIndexViewModel
        {
            StatusFilter = status,
            PatientId = patientId,
            CalendarView = view,
            ReferenceDate = referenceDate,
            Buckets = buckets,
            Statuses = Enum.GetValues<AppointmentStatus>(),
            PeriodLabel = periodLabel,
            PreviousDate = previousDate,
            NextDate = nextDate
        };

        return View(vm);
    }

    public async Task<IActionResult> Create(int? patientId)
    {
        var today = DateTime.Today;
        var vm = new AppointmentFormViewModel
        {
            PatientId = patientId ?? 0,
            AppointmentDate = today.AddHours(9),
            InvoiceDate = DateOnly.FromDateTime(today),
            DueDate = DateOnly.FromDateTime(today.AddDays(30)),
            FirstInstallmentDate = DateOnly.FromDateTime(today.AddDays(30))
        };

        await PopulateFormOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentFormViewModel vm)
    {
        await ValidateBillingAsync(vm);
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(vm);
            return View(vm);
        }

        var invoiceId = await EnsureInvoiceAsync(vm);

        var appointment = new Appointment
        {
            PatientId = vm.PatientId,
            InvoiceId = invoiceId,
            AppointmentDate = vm.AppointmentDate,
            Purpose = vm.Purpose?.Trim() ?? string.Empty,
            Status = vm.Status,
            Notes = vm.Notes,
            SelectedTeethData = TeethSelectionSerializer.Serialize(vm.SelectedTeeth),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Randevu başarıyla oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null)
        {
            return NotFound();
        }

        var vm = new AppointmentFormViewModel
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            InvoiceId = appointment.InvoiceId,
            AppointmentDate = appointment.AppointmentDate,
            Purpose = appointment.Purpose,
            Status = appointment.Status,
            Notes = appointment.Notes,
            SelectedTeeth = TeethSelectionSerializer.Parse(appointment.SelectedTeethData),
            InvoiceDate = DateOnly.FromDateTime(appointment.AppointmentDate),
            DueDate = DateOnly.FromDateTime(appointment.AppointmentDate.Date.AddDays(30)),
            FirstInstallmentDate = DateOnly.FromDateTime(appointment.AppointmentDate.Date.AddDays(30))
        };

        await PopulateFormOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppointmentFormViewModel vm)
    {
        if (id != vm.Id)
        {
            return BadRequest();
        }

        await ValidateBillingAsync(vm);
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(vm);
            return View(vm);
        }

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null)
        {
            return NotFound();
        }

        var invoiceId = await EnsureInvoiceAsync(vm);

        appointment.PatientId = vm.PatientId;
        appointment.InvoiceId = invoiceId;
        appointment.AppointmentDate = vm.AppointmentDate;
        appointment.Purpose = vm.Purpose?.Trim() ?? string.Empty;
        appointment.Status = vm.Status;
        appointment.Notes = vm.Notes;
        appointment.SelectedTeethData = TeethSelectionSerializer.Serialize(vm.SelectedTeeth);
        appointment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Randevu bilgileri güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null)
        {
            return NotFound();
        }

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Randevu iptal edildi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateFormOptionsAsync(AppointmentFormViewModel vm)
    {
        vm.Patients = await GetPatientSelectList();
        vm.PurposeSuggestions = AppointmentPurposeCatalog.Default;
        vm.Invoices = await _db.Invoices
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Select(i => new AppointmentInvoiceOptionViewModel
            {
                Id = i.Id,
                PatientId = i.PatientId,
                Label = $"Fatura #{i.Id} - {i.InvoiceDate:dd.MM.yyyy} - {i.TotalAmount:C} - {i.Status.GetDisplayName()}"
            })
            .ToListAsync();
    }

    private async Task<IEnumerable<SelectListItem>> GetPatientSelectList()
    {
        return await _db.Patients
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.FullName)
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName })
            .ToListAsync();
    }

    private async Task ValidateBillingAsync(AppointmentFormViewModel vm)
    {
        if (vm.CreateInvoice)
        {
            vm.InvoiceId = null;

            if (!vm.InvoiceTotalAmount.HasValue || vm.InvoiceTotalAmount.Value <= 0)
            {
                ModelState.AddModelError(nameof(AppointmentFormViewModel.InvoiceTotalAmount), "Yeni fatura için toplam tutar zorunludur.");
            }

            if (vm.EnableInstallments && !vm.FirstInstallmentDate.HasValue)
            {
                ModelState.AddModelError(nameof(AppointmentFormViewModel.FirstInstallmentDate), "İlk taksit tarihi zorunludur.");
            }

            return;
        }

        if (!vm.InvoiceId.HasValue)
        {
            return;
        }

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == vm.InvoiceId.Value)
            .Select(i => new { i.PatientId })
            .FirstOrDefaultAsync();

        if (invoice is null || invoice.PatientId != vm.PatientId)
        {
            vm.InvoiceId = null;
            ModelState.AddModelError(nameof(AppointmentFormViewModel.InvoiceId), "Seçilen fatura seçili hastaya ait değil.");
        }
    }

    private async Task<int?> EnsureInvoiceAsync(AppointmentFormViewModel vm)
    {
        if (!vm.CreateInvoice)
        {
            return vm.InvoiceId;
        }

        var invoice = new Invoice
        {
            PatientId = vm.PatientId,
            InvoiceDate = vm.InvoiceDate,
            DueDate = vm.DueDate,
            TotalAmount = vm.InvoiceTotalAmount!.Value,
            Status = InvoiceStatus.Issued,
            Notes = vm.InvoiceNotes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        await CreateInstallmentsAsync(invoice, vm);

        return invoice.Id;
    }

    private async Task CreateInstallmentsAsync(Invoice invoice, AppointmentFormViewModel vm)
    {
        if (!vm.EnableInstallments || !vm.FirstInstallmentDate.HasValue)
        {
            return;
        }

        var installmentAmount = Math.Round(invoice.TotalAmount / vm.InstallmentCount, 2, MidpointRounding.AwayFromZero);
        var runningTotal = 0m;

        for (var installmentNumber = 1; installmentNumber <= vm.InstallmentCount; installmentNumber++)
        {
            var amount = installmentNumber == vm.InstallmentCount
                ? invoice.TotalAmount - runningTotal
                : installmentAmount;

            runningTotal += amount;

            _db.Payments.Add(new Payment
            {
                InvoiceId = invoice.Id,
                PaymentDate = vm.FirstInstallmentDate.Value.AddMonths((installmentNumber - 1) * vm.InstallmentIntervalMonths),
                Amount = amount,
                PaymentMethod = PaymentMethod.Other,
                Notes = "Randevu ekranından oluşturulan taksit kaydı",
                IsPlanned = true,
                IsSettled = false,
                InstallmentNumber = installmentNumber,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    private static (DateTime Start, DateTime End, DateTime PreviousDate, DateTime NextDate, List<AppointmentBucketViewModel> Buckets, string Label) BuildCalendarFrame(AppointmentCalendarView view, DateTime referenceDate)
    {
        return view switch
        {
            AppointmentCalendarView.Weekly => BuildWeeklyFrame(referenceDate),
            AppointmentCalendarView.Monthly => BuildMonthlyFrame(referenceDate),
            AppointmentCalendarView.Yearly => BuildYearlyFrame(referenceDate),
            _ => BuildDailyFrame(referenceDate)
        };
    }

    private static (DateTime, DateTime, DateTime, DateTime, List<AppointmentBucketViewModel>, string) BuildDailyFrame(DateTime referenceDate)
    {
        var start = referenceDate.Date;
        var end = start.AddDays(1);
        return (start, end, start.AddDays(-1), start.AddDays(1), new List<AppointmentBucketViewModel>
        {
            new()
            {
                Start = start,
                Label = start.ToString("dddd, dd MMMM yyyy")
            }
        }, start.ToString("D"));
    }

    private static (DateTime, DateTime, DateTime, DateTime, List<AppointmentBucketViewModel>, string) BuildWeeklyFrame(DateTime referenceDate)
    {
        var diff = ((int)referenceDate.DayOfWeek + 6) % 7;
        var start = referenceDate.AddDays(-diff).Date;
        var end = start.AddDays(7);
        var buckets = Enumerable.Range(0, 7)
            .Select(offset => new AppointmentBucketViewModel
            {
                Start = start.AddDays(offset),
                Label = start.AddDays(offset).ToString("dddd, dd MMMM")
            })
            .ToList();

        return (start, end, start.AddDays(-7), start.AddDays(7), buckets, $"{start:dd MMM} - {end.AddDays(-1):dd MMM yyyy}");
    }

    private static (DateTime, DateTime, DateTime, DateTime, List<AppointmentBucketViewModel>, string) BuildMonthlyFrame(DateTime referenceDate)
    {
        var start = new DateTime(referenceDate.Year, referenceDate.Month, 1);
        var end = start.AddMonths(1);
        var buckets = Enumerable.Range(0, DateTime.DaysInMonth(start.Year, start.Month))
            .Select(offset => new AppointmentBucketViewModel
            {
                Start = start.AddDays(offset),
                Label = start.AddDays(offset).ToString("dd MMMM dddd")
            })
            .ToList();

        return (start, end, start.AddMonths(-1), start.AddMonths(1), buckets, start.ToString("MMMM yyyy"));
    }

    private static (DateTime, DateTime, DateTime, DateTime, List<AppointmentBucketViewModel>, string) BuildYearlyFrame(DateTime referenceDate)
    {
        var start = new DateTime(referenceDate.Year, 1, 1);
        var end = start.AddYears(1);
        var buckets = Enumerable.Range(1, 12)
            .Select(month => new AppointmentBucketViewModel
            {
                Start = new DateTime(referenceDate.Year, month, 1),
                Label = new DateTime(referenceDate.Year, month, 1).ToString("MMMM yyyy")
            })
            .ToList();

        return (start, end, start.AddYears(-1), start.AddYears(1), buckets, referenceDate.Year.ToString());
    }

    private static DateTime GetBucketEnd(AppointmentBucketViewModel bucket, AppointmentCalendarView view)
    {
        return view switch
        {
            AppointmentCalendarView.Yearly => bucket.Start.AddMonths(1),
            _ => bucket.Start.AddDays(1)
        };
    }
}
