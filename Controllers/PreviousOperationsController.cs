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
public class PreviousOperationsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PreviousOperationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? patientId, string? search)
    {
        Patient? patient = null;
        if (patientId.HasValue)
        {
            patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId.Value);
            if (patient == null) return NotFound();
        }

        var query = _db.PreviousOperations
            .Include(o => o.Patient)
            .AsQueryable();

        if (patientId.HasValue)
        {
            query = query.Where(o => o.PatientId == patientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o =>
                o.Title.Contains(search) ||
                (o.Diagnosis != null && o.Diagnosis.Contains(search)) ||
                (o.Procedures != null && o.Procedures.Contains(search)) ||
                (o.Prescriptions != null && o.Prescriptions.Contains(search)) ||
                (o.Notes != null && o.Notes.Contains(search)) ||
                (o.Patient != null && o.Patient.FullName.Contains(search)));
        }

        var vm = new PreviousOperationsIndexViewModel
        {
            Patient = patient,
            PatientId = patientId,
            Search = search,
            Operations = await query
                .OrderByDescending(o => o.Date)
                .ThenByDescending(o => o.UpdatedAt)
                .ToListAsync()
        };

        return View(vm);
    }

    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new PreviousOperationFormViewModel
        {
            PatientId = patientId ?? 0,
            Date = DateOnly.FromDateTime(DateTime.Today)
        };

        vm.Patients = await GetPatientSelectList();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PreviousOperationFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var operation = new PreviousOperation
        {
            PatientId = vm.PatientId,
            Date = vm.Date,
            Title = vm.Title.Trim(),
            Diagnosis = vm.Diagnosis,
            Procedures = vm.Procedures,
            Prescriptions = vm.Prescriptions,
            Notes = vm.Notes,
            SelectedTeethData = TeethSelectionSerializer.Serialize(vm.SelectedTeeth),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PreviousOperations.Add(operation);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Önceki işlem kaydı oluşturuldu.";
        return RedirectToAction(nameof(Index), new { patientId = operation.PatientId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var operation = await _db.PreviousOperations.FindAsync(id);
        if (operation == null) return NotFound();

        var vm = new PreviousOperationFormViewModel
        {
            Id = operation.Id,
            PatientId = operation.PatientId,
            Date = operation.Date,
            Title = operation.Title,
            Diagnosis = operation.Diagnosis,
            Procedures = operation.Procedures,
            Prescriptions = operation.Prescriptions,
            Notes = operation.Notes,
            SelectedTeeth = TeethSelectionSerializer.Parse(operation.SelectedTeethData)
        };

        vm.Patients = await GetPatientSelectList();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PreviousOperationFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            vm.Patients = await GetPatientSelectList();
            return View(vm);
        }

        var operation = await _db.PreviousOperations.FindAsync(id);
        if (operation == null) return NotFound();

        operation.PatientId = vm.PatientId;
        operation.Date = vm.Date;
        operation.Title = vm.Title.Trim();
        operation.Diagnosis = vm.Diagnosis;
        operation.Procedures = vm.Procedures;
        operation.Prescriptions = vm.Prescriptions;
        operation.Notes = vm.Notes;
        operation.SelectedTeethData = TeethSelectionSerializer.Serialize(vm.SelectedTeeth);
        operation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Önceki işlem kaydı güncellendi.";
        return RedirectToAction(nameof(Index), new { patientId = operation.PatientId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var operation = await _db.PreviousOperations.FindAsync(id);
        if (operation == null) return NotFound();

        var patientId = operation.PatientId;
        _db.PreviousOperations.Remove(operation);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Önceki işlem kaydı silindi.";
        return RedirectToAction(nameof(Index), new { patientId });
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
