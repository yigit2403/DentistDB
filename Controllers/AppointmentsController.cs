using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[Authorize]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AppointmentsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET: /Appointments
    public async Task<IActionResult> Index(string? status, int? patientId)
    {
        var query = _db.Appointments.Include(a => a.Patient).AsQueryable();

        if (Enum.TryParse<AppointmentStatus>(status, out var parsedStatus))
            query = query.Where(a => a.Status == parsedStatus);

        if (patientId.HasValue)
            query = query.Where(a => a.PatientId == patientId);

        ViewBag.StatusFilter = status;
        ViewBag.Statuses = Enum.GetNames<AppointmentStatus>();
        return View(await query.OrderByDescending(a => a.AppointmentDate).ToListAsync());
    }

    // GET: /Appointments/Create
    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new AppointmentFormViewModel
        {
            PatientId = patientId ?? 0,
            AppointmentDate = DateTime.Today.AddHours(9),
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    // POST: /Appointments/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var appointment = new Appointment
        {
            PatientId = vm.PatientId,
            AppointmentDate = vm.AppointmentDate,
            Purpose = vm.Purpose,
            Status = vm.Status,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Appointment created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Appointments/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var a = await _db.Appointments.FindAsync(id);
        if (a == null) return NotFound();

        var vm = new AppointmentFormViewModel
        {
            Id = a.Id,
            PatientId = a.PatientId,
            AppointmentDate = a.AppointmentDate,
            Purpose = a.Purpose,
            Status = a.Status,
            Notes = a.Notes,
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    // POST: /Appointments/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppointmentFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        appointment.PatientId = vm.PatientId;
        appointment.AppointmentDate = vm.AppointmentDate;
        appointment.Purpose = vm.Purpose;
        appointment.Status = vm.Status;
        appointment.Notes = vm.Notes;
        appointment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Appointment updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Appointments/Cancel/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Appointment cancelled.";
        return RedirectToAction(nameof(Index));
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
