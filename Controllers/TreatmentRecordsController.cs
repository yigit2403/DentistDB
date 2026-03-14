using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;
using DentistDB.Extensions;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class TreatmentRecordsController : Controller
{
    private readonly ApplicationDbContext _db;

    public TreatmentRecordsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new TreatmentRecordFormViewModel
        {
            PatientId = patientId ?? 0,
            Date = DateOnly.FromDateTime(DateTime.Today)
        };

        vm.Patients = await GetPatientSelectList();
        return View(vm);
    }

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
            Notes = TreatmentRecordTeethSerializer.Merge(vm.Notes, vm.SelectedTeeth),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TreatmentRecords.Add(record);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tedavi kaydı kaydedildi.";
        return RedirectToAction("Details", "Patients", new { id = record.PatientId });
    }

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
            Notes = TreatmentRecordTeethSerializer.StripMetadata(record.Notes),
            SelectedTeeth = TreatmentRecordTeethSerializer.ParseSelectedTeeth(record.Notes)
        };

        vm.Patients = await GetPatientSelectList();
        return View(vm);
    }

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
        record.Notes = TreatmentRecordTeethSerializer.Merge(vm.Notes, vm.SelectedTeeth);
        record.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Tedavi kaydı güncellendi.";
        return RedirectToAction("Details", "Patients", new { id = record.PatientId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _db.TreatmentRecords.FindAsync(id);
        if (record == null) return NotFound();

        var patientId = record.PatientId;
        _db.TreatmentRecords.Remove(record);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tedavi kaydı silindi.";
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
