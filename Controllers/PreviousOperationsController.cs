using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

public class PreviousOperationsController : ClinicControllerBase
{
    private const int PageSize = 20;
    private readonly ISettingsService _settings;

    public PreviousOperationsController(ApplicationDbContext db, ISettingsService settings) : base(db)
    {
        _settings = settings;
    }

    /// <summary>Printable prescription sheet for a treatment record.</summary>
    public async Task<IActionResult> Prescription(int id)
    {
        var operation = await Db.PreviousOperations.AsNoTracking().Include(o => o.Patient).FirstOrDefaultAsync(o => o.Id == id);
        if (operation?.Patient == null) return NotFound();

        var clinic = await _settings.GetClinicSettingsAsync();
        return View(new PrescriptionPrintViewModel
        {
            Operation = operation,
            Patient = operation.Patient,
            ClinicName = clinic.ClinicName,
            Identity = await _settings.GetClinicIdentityAsync()
        });
    }

    public async Task<IActionResult> Index(int? patientId, string? search, int pageNumber = 1)
    {
        Patient? patient = null;
        if (patientId.HasValue)
        {
            patient = await Db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId.Value);
            if (patient == null) return NotFound();
        }

        var query = Db.PreviousOperations.AsNoTracking().Include(o => o.Patient).AsQueryable();

        if (patientId.HasValue)
        {
            query = query.Where(o => o.PatientId == patientId.Value);
        }

        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // Both sides are Turkish-folded, so "çekim", "Çekim" and "cekim" all match.
            var normalizedPattern = $"%{SearchNormalizer.Normalize(searchTerm)}%";
            query = query.Where(o =>
                EF.Functions.Like(o.SearchIndex, normalizedPattern) ||
                (o.Patient != null && EF.Functions.Like(o.Patient.SearchIndex, normalizedPattern)));
        }

        var vm = new PreviousOperationsIndexViewModel
        {
            Patient = patient,
            PatientId = patientId,
            Search = searchTerm,
            Operations = await PaginatedList<PreviousOperation>.CreateAsync(
                query.OrderByDescending(o => o.Date).ThenByDescending(o => o.Id), pageNumber, PageSize)
        };

        return View(vm);
    }

    public async Task<IActionResult> Create(int? patientId, string? returnUrl)
    {
        var vm = new PreviousOperationFormViewModel
        {
            PatientId = patientId ?? 0,
            Date = DateOnly.FromDateTime(DateTime.Today),
            ReturnUrl = SafeReturnUrl(returnUrl)
        };

        await PopulateAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PreviousOperationFormViewModel vm)
    {
        await ValidateAsync(vm);
        if (!ModelState.IsValid)
        {
            await PopulateAsync(vm);
            return View(vm);
        }

        var operation = new PreviousOperation
        {
            PatientId = vm.PatientId,
            CreatedAt = DateTime.UtcNow
        };
        Apply(operation, vm);

        Db.PreviousOperations.Add(operation);
        await Db.SaveChangesAsync();
        Success("Tedavi kaydı oluşturuldu.");
        return RedirectToReturnUrlOr(vm.ReturnUrl, RedirectToAction("Details", "Patients", new { id = operation.PatientId, tab = "treatments" }));
    }

    public async Task<IActionResult> Edit([FromRoute] int id, string? returnUrl)
    {
        var operation = await Db.PreviousOperations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
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
            SelectedTeeth = operation.SelectedTeethData,
            ReturnUrl = SafeReturnUrl(returnUrl)
        };

        await PopulateAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, PreviousOperationFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        await ValidateAsync(vm);
        if (!ModelState.IsValid)
        {
            await PopulateAsync(vm);
            return View(vm);
        }

        var operation = await Db.PreviousOperations.FindAsync(id);
        if (operation == null) return NotFound();

        operation.PatientId = vm.PatientId;
        Apply(operation, vm);

        await Db.SaveChangesAsync();
        Success("Tedavi kaydı güncellendi.");
        return RedirectToReturnUrlOr(vm.ReturnUrl, RedirectToAction("Details", "Patients", new { id = operation.PatientId, tab = "treatments" }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var operation = await Db.PreviousOperations.FindAsync(id);
        if (operation == null) return NotFound();

        var patientId = operation.PatientId;
        Db.PreviousOperations.Remove(operation);
        await Db.SaveChangesAsync();
        Success("Tedavi kaydı silindi.");
        return RedirectToReturnUrlOr(returnUrl, RedirectToAction("Details", "Patients", new { id = patientId, tab = "treatments" }));
    }

    private async Task PopulateAsync(PreviousOperationFormViewModel vm)
    {
        vm.Patients = await GetPatientSelectListAsync(vm.PatientId == 0 ? null : vm.PatientId);
        vm.TitleSuggestions = await Db.Procedures.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => p.Name)
            .ToListAsync();

        if (vm.PatientId > 0)
        {
            vm.PatientName = await Db.Patients.Where(p => p.Id == vm.PatientId).Select(p => p.FullName).FirstOrDefaultAsync();
        }
    }

    /// <summary>Rules the data annotations cannot express: the patient must exist and the date cannot be in the future.</summary>
    private async Task ValidateAsync(PreviousOperationFormViewModel vm)
    {
        if (vm.PatientId > 0 && !await Db.Patients.AnyAsync(p => p.Id == vm.PatientId))
        {
            ModelState.AddModelError(nameof(vm.PatientId), "Seçilen hasta bulunamadı.");
        }

        if (vm.Date > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(vm.Date), "Tedavi tarihi gelecekte olamaz; ileri tarihli işler için randevu oluşturun.");
        }
    }

    private static void Apply(PreviousOperation operation, PreviousOperationFormViewModel vm)
    {
        operation.Date = vm.Date;
        operation.Title = vm.Title.Trim();
        operation.Diagnosis = Clean(vm.Diagnosis);
        operation.Procedures = Clean(vm.Procedures);
        operation.Prescriptions = Clean(vm.Prescriptions);
        operation.Notes = Clean(vm.Notes);
        operation.SelectedTeethData = TeethSelectionSerializer.Normalize(vm.SelectedTeeth);
        operation.SearchIndex = SearchNormalizer.BuildOperationIndex(operation);
        operation.UpdatedAt = DateTime.UtcNow;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
