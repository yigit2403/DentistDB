using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[Authorize]
public class TreatmentRecordsController : Controller
{
    private readonly ApplicationDbContext _db;

    public TreatmentRecordsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET: /TreatmentRecords/Create?patientId=5
    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new TreatmentRecordFormViewModel
        {
            PatientId = patientId ?? 0,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    // POST: /TreatmentRecords/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TreatmentRecordFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var record = new TreatmentRecord
        {
            PatientId = vm.PatientId,
            Date = vm.Date,
            Diagnosis = vm.Diagnosis,
            Procedures = vm.Procedures,
            Prescriptions = vm.Prescriptions,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TreatmentRecords.Add(record);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tedavi kaydi kaydedildi.";
        return RedirectToAction("Details", "Patients", new { id = record.PatientId });
    }

    // GET: /TreatmentRecords/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var record = await _db.TreatmentRecords.FindAsync(id);
        if (record == null) return NotFound();

        var vm = new TreatmentRecordFormViewModel
        {
            Id = record.Id,
            PatientId = record.PatientId,
            Date = record.Date,
            Diagnosis = record.Diagnosis,
            Procedures = record.Procedures,
            Prescriptions = record.Prescriptions,
            Notes = record.Notes,
            Patients = await GetPatientSelectList()
        };
        return View(vm);
    }

    // POST: /TreatmentRecords/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TreatmentRecordFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var record = await _db.TreatmentRecords.FindAsync(id);
        if (record == null) return NotFound();

        record.PatientId = vm.PatientId;
        record.Date = vm.Date;
        record.Diagnosis = vm.Diagnosis;
        record.Procedures = vm.Procedures;
        record.Prescriptions = vm.Prescriptions;
        record.Notes = vm.Notes;
        record.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Tedavi kaydi guncellendi.";
        return RedirectToAction("Details", "Patients", new { id = record.PatientId });
    }

    // POST: /TreatmentRecords/Delete/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _db.TreatmentRecords.FindAsync(id);
        if (record == null) return NotFound();

        var patientId = record.PatientId;
        _db.TreatmentRecords.Remove(record);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tedavi kaydi silindi.";
        return RedirectToAction("Details", "Patients", new { id = patientId });
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
