using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class PatientsController : Controller
{
    private const int PageSize = 20;
    private readonly ApplicationDbContext _db;

    public PatientsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, bool showArchived = false, int pageNumber = 1)
    {
        var query = _db.Patients.AsQueryable();

        if (!showArchived)
            query = query.Where(p => !p.IsArchived);

        var searchTerm = search?.Trim();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(p =>
                EF.Functions.Like(p.FullName, pattern) ||
                EF.Functions.Like(p.Tckn, pattern) ||
                (p.Phone != null && EF.Functions.Like(p.Phone, pattern)) ||
                (p.Email != null && EF.Functions.Like(p.Email, pattern)));
        }

        ViewBag.Search = searchTerm;
        ViewBag.ShowArchived = showArchived;

        return View(await PaginatedList<Patient>.CreateAsync(query.OrderBy(p => p.FullName), pageNumber, PageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var patient = await _db.Patients
            .Include(p => p.Appointments.OrderByDescending(a => a.AppointmentDate))
            .Include(p => p.PreviousOperations.OrderByDescending(o => o.Date))
            .Include(p => p.Scans.OrderByDescending(s => s.ScanDate))
            .Include(p => p.Invoices.OrderByDescending(i => i.InvoiceDate))
                .ThenInclude(i => i.Payments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null) return NotFound();
        return View(patient);
    }

    public IActionResult Create() => View(new PatientFormViewModel
    {
        ArrivalDate = DateOnly.FromDateTime(DateTime.Today)
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PatientFormViewModel vm)
    {
        await ValidateTcknAsync(vm.Tckn);
        ValidatePhoto(vm.PhotoFile);
        if (!ModelState.IsValid) return View(vm);

        var patient = new Patient
        {
            FullName = vm.FullName,
            Phone = vm.Phone,
            Tckn = vm.Tckn,
            Email = vm.Email,
            BirthDate = vm.BirthDate,
            ArrivalDate = vm.ArrivalDate,
            Address = vm.Address,
            Notes = vm.Notes,
            MedicalAlerts = vm.MedicalAlerts,
            PhotoBase64 = await ConvertPhotoToBase64Async(vm.PhotoFile),
            PhotoContentType = vm.PhotoFile?.ContentType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Hasta kaydı başarıyla oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        var vm = new PatientFormViewModel
        {
            Id = patient.Id,
            FullName = patient.FullName,
            Phone = patient.Phone,
            Tckn = patient.Tckn,
            Email = patient.Email,
            BirthDate = patient.BirthDate,
            ArrivalDate = patient.ArrivalDate,
            Address = patient.Address,
            Notes = patient.Notes,
            MedicalAlerts = patient.MedicalAlerts,
            ExistingPhotoBase64 = patient.PhotoBase64,
            ExistingPhotoContentType = patient.PhotoContentType,
            IsArchived = patient.IsArchived
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PatientFormViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        await ValidateTcknAsync(vm.Tckn, id);
        ValidatePhoto(vm.PhotoFile);

        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        if (!ModelState.IsValid)
        {
            vm.ExistingPhotoBase64 = patient.PhotoBase64;
            vm.ExistingPhotoContentType = patient.PhotoContentType;
            return View(vm);
        }

        patient.FullName = vm.FullName;
        patient.Phone = vm.Phone;
        patient.Tckn = vm.Tckn;
        patient.Email = vm.Email;
        patient.BirthDate = vm.BirthDate;
        patient.ArrivalDate = vm.ArrivalDate;
        patient.Address = vm.Address;
        patient.Notes = vm.Notes;
        patient.MedicalAlerts = vm.MedicalAlerts;
        if (vm.PhotoFile is not null)
        {
            patient.PhotoBase64 = await ConvertPhotoToBase64Async(vm.PhotoFile);
            patient.PhotoContentType = vm.PhotoFile.ContentType;
        }
        patient.IsArchived = vm.IsArchived;
        patient.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Hasta bilgileri başarıyla güncellendi.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.IsArchived = true;
        patient.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{patient.FullName} arşive taşındı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.IsArchived = false;
        patient.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{patient.FullName} kaydı tekrar aktif edildi.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    private async Task ValidateTcknAsync(string tckn, int? currentPatientId = null)
    {
        var exists = await _db.Patients.AnyAsync(p => p.Tckn == tckn && (!currentPatientId.HasValue || p.Id != currentPatientId.Value));
        if (exists)
        {
            ModelState.AddModelError(nameof(PatientFormViewModel.Tckn), "Bu TCKN başka bir hastada kayıtlı.");
        }
    }

    private void ValidatePhoto(IFormFile? photoFile)
    {
        if (photoFile is null || photoFile.Length == 0)
        {
            return;
        }

        if (photoFile.Length > 2 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(PatientFormViewModel.PhotoFile), "Fotoğraf en fazla 2 MB olabilir.");
        }

        if (!photoFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(PatientFormViewModel.PhotoFile), "Lütfen geçerli bir görsel dosyası yükleyin.");
        }
    }

    private static async Task<string?> ConvertPhotoToBase64Async(IFormFile? photoFile)
    {
        if (photoFile is null || photoFile.Length == 0)
        {
            return null;
        }

        await using var stream = new MemoryStream();
        await photoFile.CopyToAsync(stream);
        return Convert.ToBase64String(stream.ToArray());
    }
}
