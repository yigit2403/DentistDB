using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[Authorize]
public class PatientsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PatientsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET: /Patients
    public async Task<IActionResult> Index(string? search, bool showArchived = false)
    {
        var query = _db.Patients.AsQueryable();

        if (!showArchived)
            query = query.Where(p => !p.IsArchived);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.FullName.Contains(search) ||
                (p.Phone != null && p.Phone.Contains(search)) ||
                (p.Email != null && p.Email.Contains(search)));

        ViewBag.Search = search;
        ViewBag.ShowArchived = showArchived;

        return View(await query.OrderBy(p => p.FullName).ToListAsync());
    }

    // GET: /Patients/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var patient = await _db.Patients
            .Include(p => p.Appointments.OrderByDescending(a => a.AppointmentDate))
            .Include(p => p.TreatmentRecords.OrderByDescending(t => t.Date))
            .Include(p => p.Scans.OrderByDescending(s => s.ScanDate))
            .Include(p => p.Invoices.OrderByDescending(i => i.InvoiceDate))
                .ThenInclude(i => i.Payments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null) return NotFound();
        return View(patient);
    }

    // GET: /Patients/Create
    public IActionResult Create() => View(new PatientFormViewModel());

    // POST: /Patients/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PatientFormViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var patient = new Patient
        {
            FullName = vm.FullName,
            Phone = vm.Phone,
            Email = vm.Email,
            BirthDate = vm.BirthDate,
            Address = vm.Address,
            Notes = vm.Notes,
            MedicalAlerts = vm.MedicalAlerts,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Hasta kaydi basariyla olusturuldu.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    // GET: /Patients/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        var vm = new PatientFormViewModel
        {
            Id = patient.Id,
            FullName = patient.FullName,
            Phone = patient.Phone,
            Email = patient.Email,
            BirthDate = patient.BirthDate,
            Address = patient.Address,
            Notes = patient.Notes,
            MedicalAlerts = patient.MedicalAlerts,
            IsArchived = patient.IsArchived
        };
        return View(vm);
    }

    // POST: /Patients/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PatientFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid) return View(vm);

        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.FullName = vm.FullName;
        patient.Phone = vm.Phone;
        patient.Email = vm.Email;
        patient.BirthDate = vm.BirthDate;
        patient.Address = vm.Address;
        patient.Notes = vm.Notes;
        patient.MedicalAlerts = vm.MedicalAlerts;
        patient.IsArchived = vm.IsArchived;
        patient.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Hasta bilgileri basariyla guncellendi.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    // POST: /Patients/Archive/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.IsArchived = true;
        patient.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{patient.FullName} arsive tasindi.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Patients/Restore/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.IsArchived = false;
        patient.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{patient.FullName} kaydi tekrar aktif edildi.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }
}
