using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

public class AppointmentsController : ClinicControllerBase
{
    private readonly ISettingsService _settings;

    public AppointmentsController(ApplicationDbContext db, ISettingsService settings) : base(db)
    {
        _settings = settings;
    }

    public async Task<IActionResult> Index(AppointmentStatus? status, int? patientId, AppointmentCalendarView view = AppointmentCalendarView.Daily, DateTime? date = null)
    {
        var referenceDate = (date ?? DateTime.Today).Date;
        var clinic = await _settings.GetClinicSettingsAsync();

        var vm = new AppointmentIndexViewModel
        {
            StatusFilter = status,
            PatientId = patientId,
            CalendarView = view,
            ReferenceDate = referenceDate,
            DayStart = clinic.DayStart,
            DayEnd = clinic.DayEnd
        };

        BuildCalendarFrame(vm);

        var query = Db.Appointments.AsNoTracking().Include(a => a.Patient)
            .Where(a => a.AppointmentDate >= vm.PeriodStart && a.AppointmentDate < vm.PeriodEnd);

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (patientId.HasValue)
        {
            query = query.Where(a => a.PatientId == patientId.Value);
            vm.PatientName = await Db.Patients.Where(p => p.Id == patientId.Value).Select(p => p.FullName).FirstOrDefaultAsync();
        }

        vm.Appointments = await query.OrderBy(a => a.AppointmentDate).ThenBy(a => a.Id).ToListAsync();

        var byDay = vm.Appointments.ToLookup(a => a.AppointmentDate.Date);
        foreach (var day in vm.Days)
        {
            foreach (var appointment in byDay[day.Date])
            {
                day.Appointments.Add(appointment);
            }
        }

        return View(vm);
    }

    /// <summary>Printable list of one day's appointments with phone numbers.</summary>
    public async Task<IActionResult> PrintDay(DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        var clinic = await _settings.GetClinicSettingsAsync();

        var vm = new DaySheetViewModel
        {
            Date = day,
            ClinicName = clinic.ClinicName,
            Appointments = await Db.Appointments.AsNoTracking()
                .Include(a => a.Patient)
                .Where(a => a.AppointmentDate >= day && a.AppointmentDate < day.AddDays(1) && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync()
        };

        return View(vm);
    }

    public async Task<IActionResult> Create(int? patientId, DateTime? date, string? returnUrl, string? purpose, string? teeth, int? duration, int? planItemId)
    {
        // The query string binds into ModelState ("date" = "2026-09-10T09:15"), and the input tag helper
        // prefers that raw value over the formatted model value, which leaves a type="date" field empty.
        ModelState.Clear();

        var clinic = await _settings.GetClinicSettingsAsync();
        var start = date.HasValue
            ? AppointmentRules.RoundToSlot(date.Value)
            : DateTime.Today.Add(clinic.DayStart.ToTimeSpan());

        if (date.HasValue && date.Value.TimeOfDay == TimeSpan.Zero)
        {
            start = date.Value.Date.Add(clinic.DayStart.ToTimeSpan());
        }

        var vm = new AppointmentFormViewModel
        {
            PatientId = patientId ?? 0,
            Date = DateOnly.FromDateTime(start),
            Time = TimeOnly.FromDateTime(start),
            Purpose = purpose,
            SelectedTeeth = TeethSelectionSerializer.Normalize(teeth),
            DurationMinutes = duration is >= 5 and <= 600 ? duration.Value : Appointment.DefaultDurationMinutes,
            PlanItemId = planItemId,
            ReturnUrl = SafeReturnUrl(returnUrl)
        };

        if (planItemId.HasValue)
        {
            var item = await Db.TreatmentPlanItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == planItemId.Value);
            if (item != null)
            {
                vm.PatientId = item.PatientId;
                vm.Purpose ??= item.Description;
                vm.SelectedTeeth ??= TeethSelectionSerializer.Normalize(item.ToothNumbers);
                vm.ReturnUrl ??= Url.Action("Details", "Patients", new { id = item.PatientId, tab = "plan" });
            }
        }

        await PopulateFormOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentFormViewModel vm)
    {
        if (!await ValidateAndCheckConflictsAsync(vm))
        {
            await PopulateFormOptionsAsync(vm);
            return View(vm);
        }

        var seriesId = vm.RepeatCount > 0 ? Guid.NewGuid() : (Guid?)null;
        var created = new List<Appointment>();

        for (var i = 0; i <= vm.RepeatCount; i++)
        {
            var start = vm.RepeatIntervalDays == 30
                ? vm.StartDateTime.AddMonths(i)
                : vm.StartDateTime.AddDays(i * vm.RepeatIntervalDays);

            created.Add(new Appointment
            {
                PatientId = vm.PatientId,
                AppointmentDate = start,
                DurationMinutes = vm.DurationMinutes,
                Purpose = vm.Purpose!.Trim(),
                Status = vm.Status,
                Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
                SelectedTeethData = TeethSelectionSerializer.Normalize(vm.SelectedTeeth),
                SeriesId = seriesId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        Db.Appointments.AddRange(created);
        await Db.SaveChangesAsync();

        if (vm.PlanItemId.HasValue)
        {
            var item = await Db.TreatmentPlanItems.FirstOrDefaultAsync(t => t.Id == vm.PlanItemId.Value && t.PatientId == vm.PatientId);
            if (item != null && item.Status is TreatmentPlanStatus.Planned or TreatmentPlanStatus.Scheduled)
            {
                item.AppointmentId = created[0].Id;
                item.Status = TreatmentPlanStatus.Scheduled;
                await Db.SaveChangesAsync();
            }
        }

        var first = created[0];
        Success(created.Count == 1
            ? $"Randevu oluşturuldu: {first.AppointmentDate.ToDateTimeText()}."
            : $"{created.Count} randevu oluşturuldu ({first.AppointmentDate.ToShortDate()} – {created[^1].AppointmentDate.ToShortDate()}).");

        return RedirectToReturnUrlOr(vm.ReturnUrl, RedirectToAction(nameof(Index), new { date = first.AppointmentDate.ToString("yyyy-MM-dd") }));
    }

    public async Task<IActionResult> Edit([FromRoute] int id, string? returnUrl)
    {
        var appointment = await Db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (appointment == null) return NotFound();

        var vm = new AppointmentFormViewModel
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            Date = DateOnly.FromDateTime(appointment.AppointmentDate),
            Time = TimeOnly.FromDateTime(appointment.AppointmentDate),
            DurationMinutes = appointment.DurationMinutes,
            Purpose = appointment.Purpose,
            Status = appointment.Status,
            Notes = appointment.Notes,
            SelectedTeeth = appointment.SelectedTeethData,
            ReturnUrl = SafeReturnUrl(returnUrl)
        };

        await PopulateFormOptionsAsync(vm);
        ViewBag.SeriesCount = appointment.SeriesId.HasValue
            ? await Db.Appointments.CountAsync(a => a.SeriesId == appointment.SeriesId && a.AppointmentDate > appointment.AppointmentDate && a.Status == AppointmentStatus.Scheduled)
            : 0;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, AppointmentFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();

        var appointment = await Db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        if (!await ValidateAndCheckConflictsAsync(vm))
        {
            await PopulateFormOptionsAsync(vm);
            return View(vm);
        }

        appointment.PatientId = vm.PatientId;
        appointment.AppointmentDate = vm.StartDateTime;
        appointment.DurationMinutes = vm.DurationMinutes;
        appointment.Purpose = vm.Purpose!.Trim();
        appointment.Status = vm.Status;
        appointment.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();
        appointment.SelectedTeethData = TeethSelectionSerializer.Normalize(vm.SelectedTeeth);
        appointment.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        Success("Randevu güncellendi.");
        return RedirectToReturnUrlOr(vm.ReturnUrl, RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate.ToString("yyyy-MM-dd") }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, AppointmentStatus status, string? returnUrl)
    {
        var appointment = await Db.Appointments.Include(a => a.Patient).FirstOrDefaultAsync(a => a.Id == id);
        if (appointment == null) return NotFound();

        appointment.Status = status;
        appointment.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        Success(status switch
        {
            AppointmentStatus.Completed => $"{appointment.Patient?.FullName} randevusu tamamlandı olarak işaretlendi.",
            AppointmentStatus.Cancelled => $"{appointment.Patient?.FullName} randevusu iptal edildi.",
            AppointmentStatus.NoShow => $"{appointment.Patient?.FullName} için \"gelmedi\" kaydedildi.",
            _ => "Randevu yeniden planlandı olarak işaretlendi."
        });

        return RedirectToReturnUrlOr(returnUrl, RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate.ToString("yyyy-MM-dd") }));
    }

    /// <summary>Cancels every later scheduled appointment in the same series.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSeries(int id, string? returnUrl)
    {
        var appointment = await Db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        if (appointment.SeriesId.HasValue)
        {
            var later = await Db.Appointments
                .Where(a => a.SeriesId == appointment.SeriesId && a.AppointmentDate >= appointment.AppointmentDate && a.Status == AppointmentStatus.Scheduled)
                .ToListAsync();

            foreach (var a in later)
            {
                a.Status = AppointmentStatus.Cancelled;
                a.UpdatedAt = DateTime.UtcNow;
            }

            await Db.SaveChangesAsync();
            Success($"Seride kalan {later.Count} randevu iptal edildi.");
        }

        return RedirectToReturnUrlOr(returnUrl, RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate.ToString("yyyy-MM-dd") }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var appointment = await Db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        var date = appointment.AppointmentDate;
        Db.Appointments.Remove(appointment);
        await Db.SaveChangesAsync();
        Success("Randevu silindi.");
        return RedirectToReturnUrlOr(returnUrl, RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") }));
    }

    /// <summary>Live conflict check used by the appointment form.</summary>
    [HttpGet]
    public async Task<IActionResult> Conflicts(DateTime start, int duration, int? excludeId)
    {
        if (duration <= 0) duration = Appointment.DefaultDurationMinutes;
        var conflicts = await FindConflictsAsync(start, duration, excludeId);

        return Json(conflicts.Select(c => new
        {
            id = c.Id,
            patient = c.Patient?.FullName,
            start = c.AppointmentDate.ToString("HH:mm"),
            end = c.EndDate.ToString("HH:mm"),
            purpose = c.Purpose
        }));
    }

    private async Task<bool> ValidateAndCheckConflictsAsync(AppointmentFormViewModel vm)
    {
        if (!AppointmentPurposeCatalog.RepeatIntervals.Any(r => r.Days == vm.RepeatIntervalDays))
        {
            vm.RepeatIntervalDays = 7;
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        if (!AppointmentRules.BlocksCalendar(vm.Status))
        {
            return true;
        }

        var conflicts = await FindConflictsAsync(vm.StartDateTime, vm.DurationMinutes, vm.Id == 0 ? null : vm.Id);
        if (conflicts.Count == 0 || vm.IgnoreConflicts)
        {
            return true;
        }

        vm.Conflicts = conflicts;
        ModelState.AddModelError(string.Empty, "Seçilen saat başka bir randevuyla çakışıyor. Saati değiştirin veya çakışmayı onaylayıp kaydedin.");
        return false;
    }

    private async Task<List<Appointment>> FindConflictsAsync(DateTime start, int duration, int? excludeId)
    {
        var end = start.AddMinutes(duration);
        var windowStart = start.AddHours(-12);
        var windowEnd = end.AddHours(12);

        var candidates = await Db.Appointments.AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.AppointmentDate >= windowStart && a.AppointmentDate < windowEnd
                     && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Completed)
                     && (excludeId == null || a.Id != excludeId.Value))
            .ToListAsync();

        return candidates
            .Where(a => AppointmentRules.Overlaps(start, duration, a.AppointmentDate, a.DurationMinutes))
            .OrderBy(a => a.AppointmentDate)
            .ToList();
    }

    private async Task PopulateFormOptionsAsync(AppointmentFormViewModel vm)
    {
        vm.Patients = await GetPatientSelectListAsync(vm.PatientId == 0 ? null : vm.PatientId);

        var procedureNames = await Db.Procedures.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => p.Name)
            .ToListAsync();

        vm.PurposeSuggestions = AppointmentPurposeCatalog.Default
            .Concat(procedureNames)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (vm.PatientId > 0)
        {
            vm.PatientName = await Db.Patients.Where(p => p.Id == vm.PatientId).Select(p => p.FullName).FirstOrDefaultAsync();
        }
    }

    private static void BuildCalendarFrame(AppointmentIndexViewModel vm)
    {
        var reference = vm.ReferenceDate;
        var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

        switch (vm.CalendarView)
        {
            case AppointmentCalendarView.Weekly:
            {
                var start = StartOfWeek(reference);
                vm.PeriodStart = start;
                vm.PeriodEnd = start.AddDays(7);
                vm.PreviousDate = start.AddDays(-7);
                vm.NextDate = start.AddDays(7);
                vm.PeriodLabel = $"{start:d MMMM} – {start.AddDays(6):d MMMM yyyy}";
                vm.Days = Enumerable.Range(0, 7).Select(i => new CalendarDayViewModel { Date = start.AddDays(i) }).ToList();
                break;
            }
            case AppointmentCalendarView.Monthly:
            {
                var monthStart = new DateTime(reference.Year, reference.Month, 1);
                var gridStart = StartOfWeek(monthStart);
                var monthEnd = monthStart.AddMonths(1);
                var gridEnd = StartOfWeek(monthEnd.AddDays(6));
                if (gridEnd < monthEnd) gridEnd = gridEnd.AddDays(7);

                vm.PeriodStart = gridStart;
                vm.PeriodEnd = gridEnd;
                vm.PreviousDate = monthStart.AddMonths(-1);
                vm.NextDate = monthStart.AddMonths(1);
                vm.PeriodLabel = monthStart.ToString("MMMM yyyy", turkish);
                vm.Days = Enumerable.Range(0, (int)(gridEnd - gridStart).TotalDays)
                    .Select(i =>
                    {
                        var day = gridStart.AddDays(i);
                        return new CalendarDayViewModel { Date = day, IsCurrentPeriod = day.Month == monthStart.Month };
                    })
                    .ToList();
                break;
            }
            case AppointmentCalendarView.List:
            {
                var start = reference.Date;
                vm.PeriodStart = start;
                vm.PeriodEnd = start.AddDays(30);
                vm.PreviousDate = start.AddDays(-30);
                vm.NextDate = start.AddDays(30);
                vm.PeriodLabel = $"{start:d MMMM} – {start.AddDays(29):d MMMM yyyy}";
                vm.Days = Enumerable.Range(0, 30).Select(i => new CalendarDayViewModel { Date = start.AddDays(i) }).ToList();
                break;
            }
            default:
            {
                var start = reference.Date;
                vm.PeriodStart = start;
                vm.PeriodEnd = start.AddDays(1);
                vm.PreviousDate = start.AddDays(-1);
                vm.NextDate = start.AddDays(1);
                vm.PeriodLabel = start.ToString("d MMMM yyyy, dddd", turkish);
                vm.Days = new List<CalendarDayViewModel> { new() { Date = start } };
                break;
            }
        }
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-diff);
    }
}
