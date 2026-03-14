using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AppointmentsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? status, int? patientId)
    {
        var query = _db.Appointments.Include(a => a.Patient).AsQueryable();

        if (Enum.TryParse<AppointmentStatus>(status, out var parsedStatus))
            query = query.Where(a => a.Status == parsedStatus);

        if (patientId.HasValue)
            query = query.Where(a => a.PatientId == patientId);

        ViewBag.StatusFilter = status;
        ViewBag.Statuses = Enum.GetValues<AppointmentStatus>();
        return View(await query.OrderBy(a => a.AppointmentDate).ToListAsync());
    }

    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new AppointmentFormViewModel
        {
            PatientId = patientId ?? 0,
            AppointmentDate = DateTime.Today.AddHours(9)
        };

        await PopulateFormOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(vm);
            return View(vm);
        }

        var appointment = new Appointment
        {
            PatientId = vm.PatientId,
            AppointmentDate = vm.AppointmentDate,
            Purpose = vm.Purpose?.Trim() ?? string.Empty,
            Status = vm.Status,
            Notes = TreatmentRecordTeethSerializer.Merge(vm.Notes, vm.SelectedTeeth),
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
        if (appointment == null) return NotFound();

        var vm = new AppointmentFormViewModel
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            AppointmentDate = appointment.AppointmentDate,
            Purpose = appointment.Purpose,
            Status = appointment.Status,
            Notes = TreatmentRecordTeethSerializer.StripMetadata(appointment.Notes),
            SelectedTeeth = TreatmentRecordTeethSerializer.ParseSelectedTeeth(appointment.Notes)
        };

        await PopulateFormOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppointmentFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(vm);
            return View(vm);
        }

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        appointment.PatientId = vm.PatientId;
        appointment.AppointmentDate = vm.AppointmentDate;
        appointment.Purpose = vm.Purpose?.Trim() ?? string.Empty;
        appointment.Status = vm.Status;
        appointment.Notes = TreatmentRecordTeethSerializer.Merge(vm.Notes, vm.SelectedTeeth);
        appointment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Randevu bilgileri güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

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
    }

    private async Task<IEnumerable<SelectListItem>> GetPatientSelectList()
    {
        return await _db.Patients
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.FullName)
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName })
            .ToListAsync();
    }
}
