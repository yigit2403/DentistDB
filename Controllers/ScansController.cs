using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;
using Microsoft.Extensions.Options;

namespace DentistDB.Controllers;

[RequireAppAccount]
public class ScansController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly string _scanStoragePath;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".pdf",
        ".dcm",
        ".dicom"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "application/pdf",
        "application/dicom",
        "application/dicom+json",
        "application/octet-stream"
    };

    public ScansController(ApplicationDbContext db, IWebHostEnvironment env, IOptions<StorageOptions> storageOptions)
    {
        _db = db;
        _env = env;
        _scanStoragePath = DeploymentPaths.ResolveScanStoragePath(storageOptions.Value.ScanStoragePath, env);
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

        var ext = Path.GetExtension(vm.File.FileName);
        var contentType = vm.File.ContentType;
        if (!IsAllowedUpload(ext, contentType))
        {
            ModelState.AddModelError(nameof(vm.File), "Yalnizca JPG, PNG, PDF ve DICOM dosyalari kabul edilir.");
            var pat = await _db.Patients.FindAsync(vm.PatientId);
            ViewBag.PatientName = pat?.FullName;
            return View(vm);
        }

        // Store the file
        Directory.CreateDirectory(_scanStoragePath);

        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(_scanStoragePath, storedFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await vm.File.CopyToAsync(stream);
        }

        var scan = new Scan
        {
            PatientId = vm.PatientId,
            FileName = vm.File.FileName,
            StoredPath = storedFileName,
            ContentType = GetStoredContentType(ext, contentType),
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

    // GET: /Scans/Content/5
    public async Task<IActionResult> ContentFile(int id, bool download = false)
    {
        var scan = await _db.Scans.FindAsync(id);
        if (scan == null) return NotFound();

        var filePath = DeploymentPaths.ResolveStoredScanPath(_env, _scanStoragePath, scan.StoredPath);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var downloadName = download ? scan.FileName : null;
        return PhysicalFile(filePath, scan.ContentType, downloadName, enableRangeProcessing: true);
    }

    // POST: /Scans/Delete/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var scan = await _db.Scans.FindAsync(id);
        if (scan == null) return NotFound();

        var patientId = scan.PatientId;

        // Delete physical file
        var filePath = DeploymentPaths.ResolveStoredScanPath(_env, _scanStoragePath, scan.StoredPath);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _db.Scans.Remove(scan);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tarama silindi.";
        return RedirectToAction("Details", "Patients", new { id = patientId });
    }

    private static bool IsAllowedUpload(string? extension, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        return AllowedContentTypes.Contains(contentType);
    }

    private static string GetStoredContentType(string extension, string? contentType)
    {
        if (extension.Equals(".dcm", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".dicom", StringComparison.OrdinalIgnoreCase))
        {
            return "application/dicom";
        }

        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;
    }
}
