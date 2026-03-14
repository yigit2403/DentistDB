using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class ScansController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
    private static readonly string[] AllowedContentTypes = {
        "image/jpeg", "image/png", "application/pdf"
    };

    public ScansController(ApplicationDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _env = env;
        _config = config;
    }

    // GET: /Scans/Upload?patientId=5
    public async Task<IActionResult> Upload(int patientId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient == null) return NotFound();

        ViewBag.PatientName = patient.FullName;
        return View(new ScanUploadViewModel
        {
            PatientId = patientId,
            ScanDate = DateOnly.FromDateTime(DateTime.Today)
        });
    }

    // POST: /Scans/Upload
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(ScanUploadViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var pat = await _db.Patients.FindAsync(vm.PatientId);
            ViewBag.PatientName = pat?.FullName;
            return View(vm);
        }

        if (vm.File == null || vm.File.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.File), "Lutfen yuklemek icin bir dosya secin.");
            var pat = await _db.Patients.FindAsync(vm.PatientId);
            ViewBag.PatientName = pat?.FullName;
            return View(vm);
        }

        var ext = Path.GetExtension(vm.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext) || !AllowedContentTypes.Contains(vm.File.ContentType))
        {
            ModelState.AddModelError(nameof(vm.File), "Yalnizca JPG, PNG ve PDF dosyalari kabul edilir.");
            var pat = await _db.Patients.FindAsync(vm.PatientId);
            ViewBag.PatientName = pat?.FullName;
            return View(vm);
        }

        // Store the file
        var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "scans");
        Directory.CreateDirectory(uploadFolder);

        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadFolder, storedFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await vm.File.CopyToAsync(stream);
        }

        var scan = new Scan
        {
            PatientId = vm.PatientId,
            FileName = vm.File.FileName,
            StoredPath = Path.Combine("uploads", "scans", storedFileName),
            ContentType = vm.File.ContentType,
            FileSize = vm.File.Length,
            ScanType = vm.ScanType,
            ScanDate = vm.ScanDate,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Scans.Add(scan);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tarama basariyla yuklendi.";
        return RedirectToAction("Details", "Patients", new { id = scan.PatientId });
    }

    // GET: /Scans/View/5
    public async Task<IActionResult> View(int id)
    {
        var scan = await _db.Scans.Include(s => s.Patient).FirstOrDefaultAsync(s => s.Id == id);
        if (scan == null) return NotFound();
        return View(scan);
    }

    // POST: /Scans/Delete/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var scan = await _db.Scans.FindAsync(id);
        if (scan == null) return NotFound();

        var patientId = scan.PatientId;

        // Delete physical file
        var filePath = Path.Combine(_env.WebRootPath, scan.StoredPath);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _db.Scans.Remove(scan);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tarama silindi.";
        return RedirectToAction("Details", "Patients", new { id = patientId });
    }
}
