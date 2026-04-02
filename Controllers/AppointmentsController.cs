using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PatientFinanceService _financeService;

    public AppointmentsController(ApplicationDbContext db, PatientFinanceService financeService)
    {
        _db = db;
        _financeService = financeService;
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
            PatientMode = AppointmentPatientMode.Existing,
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
        await ValidatePatientAsync(vm);
        await ValidateFinanceAsync(vm);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(vm);
            return View(vm);
        }

        var patientId = await ResolvePatientIdAsync(vm);
        var financeAccountId = await ResolveFinanceAccountAsync(patientId, vm);

        var appointment = new Appointment
        {
            PatientId = patientId,
            InvoiceId = financeAccountId,
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
            PatientMode = AppointmentPatientMode.Existing,
            AppointmentDate = appointment.AppointmentDate,
            Purpose = appointment.Purpose,
            Status = appointment.Status,
            Notes = appointment.Notes,
            SelectedTeeth = TeethSelectionSerializer.Parse(appointment.SelectedTeethData)
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

        await ValidateExistingPatientSelectionAsync(vm.PatientId);
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

        appointment.PatientId = vm.PatientId;
        appointment.InvoiceId = await _financeService.GetAccountIdAsync(vm.PatientId);
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

        var accounts = await _db.Invoices
            .Include(i => i.Payments)
            .ToListAsync();

        vm.PatientFinances = accounts
            .Select(i => new AppointmentPatientFinanceSummaryViewModel
            {
                PatientId = i.PatientId,
                InvoiceId = i.Id,
                Balance = i.TotalAmount - i.Payments.Where(p => !p.IsPlanned || p.IsSettled).Sum(p => p.Amount),
                UnpaidInstallmentCount = i.Payments.Count(p => p.IsPlanned && !p.IsSettled)
            })
            .ToList();
    }

    private async Task<IEnumerable<SelectListItem>> GetPatientSelectList()
    {
        return await _db.Patients
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.FullName)
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName })
            .ToListAsync();
    }

    private async Task ValidatePatientAsync(AppointmentFormViewModel vm)
    {
        if (vm.PatientMode == AppointmentPatientMode.New)
        {
            vm.PatientId = 0;
            TryValidateModel(vm.NewPatient, nameof(AppointmentFormViewModel.NewPatient));

            if (string.IsNullOrWhiteSpace(vm.NewPatient.Tckn))
            {
                return;
            }

            var tcknExists = await _db.Patients.AnyAsync(p => p.Tckn == vm.NewPatient.Tckn);
            if (tcknExists)
            {
                ModelState.AddModelError($"{nameof(AppointmentFormViewModel.NewPatient)}.{nameof(PatientInputViewModel.Tckn)}", "Bu TCKN başka bir hastada kayıtlı.");
            }

            return;
        }

        await ValidateExistingPatientSelectionAsync(vm.PatientId);
    }

    private async Task ValidateExistingPatientSelectionAsync(int patientId)
    {
        if (patientId <= 0)
        {
            ModelState.AddModelError(nameof(AppointmentFormViewModel.PatientId), "Hasta seçimi zorunludur.");
            return;
        }

        var exists = await _db.Patients.AnyAsync(p => p.Id == patientId && !p.IsArchived);
        if (!exists)
        {
            ModelState.AddModelError(nameof(AppointmentFormViewModel.PatientId), "Seçilen hasta bulunamadı.");
        }
    }

    private async Task ValidateFinanceAsync(AppointmentFormViewModel vm)
    {
        if (vm.FinanceMode == AppointmentFinanceMode.None)
        {
            vm.ChargeAmount = null;
            vm.InstallmentMergeMode = null;
            return;
        }

        if (!vm.ChargeAmount.HasValue || vm.ChargeAmount.Value <= 0)
        {
            ModelState.AddModelError(nameof(AppointmentFormViewModel.ChargeAmount), "Finans işlemi için tutar zorunludur.");
        }

        if (vm.FinanceMode == AppointmentFinanceMode.PayInFull)
        {
            vm.InstallmentMergeMode = null;
            return;
        }

        if (!vm.FirstInstallmentDate.HasValue)
        {
            ModelState.AddModelError(nameof(AppointmentFormViewModel.FirstInstallmentDate), "İlk taksit tarihi zorunludur.");
        }

        if (vm.PatientMode == AppointmentPatientMode.Existing && vm.PatientId > 0)
        {
            var hasUnpaidInstallments = await _db.Payments
                .AnyAsync(p => p.Invoice != null &&
                               p.Invoice.PatientId == vm.PatientId &&
                               p.IsPlanned &&
                               !p.IsSettled);

            if (hasUnpaidInstallments && !vm.InstallmentMergeMode.HasValue)
            {
                ModelState.AddModelError(nameof(AppointmentFormViewModel.InstallmentMergeMode), "Var olan taksitler için bir birleştirme yöntemi seçin.");
            }
        }
    }

    private async Task<int> ResolvePatientIdAsync(AppointmentFormViewModel vm)
    {
        if (vm.PatientMode == AppointmentPatientMode.Existing)
        {
            return vm.PatientId;
        }

        var patient = new Patient
        {
            FullName = vm.NewPatient.FullName.Trim(),
            Phone = vm.NewPatient.Phone,
            Tckn = vm.NewPatient.Tckn,
            Email = vm.NewPatient.Email,
            BirthDate = vm.NewPatient.BirthDate,
            Address = vm.NewPatient.Address,
            Notes = vm.NewPatient.Notes,
            MedicalAlerts = vm.NewPatient.MedicalAlerts,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        return patient.Id;
    }

    private async Task<int?> ResolveFinanceAccountAsync(int patientId, AppointmentFormViewModel vm)
    {
        if (vm.FinanceMode == AppointmentFinanceMode.None)
        {
            return await _financeService.GetAccountIdAsync(patientId);
        }

        var account = await _financeService.GetOrCreateAccountAsync(patientId, vm.InvoiceDate, vm.DueDate, vm.InvoiceNotes, includePayments: true);
        _financeService.AddCharge(account, vm.ChargeAmount!.Value, vm.DueDate, vm.InvoiceNotes);

        if (vm.FinanceMode == AppointmentFinanceMode.PayInFull)
        {
            await _financeService.AddImmediatePaymentAsync(
                account,
                vm.ChargeAmount.Value,
                vm.PaymentMethod,
                vm.PaymentNotes,
                DateOnly.FromDateTime(DateTime.Today));
        }
        else
        {
            await _financeService.ApplyInstallmentChargeAsync(
                account,
                vm.ChargeAmount.Value,
                vm.FirstInstallmentDate!.Value,
                vm.InstallmentCount,
                vm.InstallmentIntervalMonths,
                vm.InstallmentMergeMode,
                "Randevu ekranından oluşturulan taksit kaydı");
        }

        return account.Id;
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
