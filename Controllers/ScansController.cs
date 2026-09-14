using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DentistDB.Controllers;

public class ScansController : ClinicControllerBase
{
    private const long MaxFileBytes = 20 * 1024 * 1024;
    private readonly IWebHostEnvironment _env;
    private readonly string _scanStoragePath;

    // Accepted by extension; the stored content type is derived here rather than trusted from the
    // browser, and the file header is checked so a renamed file of another kind is refused.
    // BMP is included because panoramic device software (e.g. Genoray Papaya / Triana) commonly exports it.
    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".pdf"] = "application/pdf"
    };

    private static bool HeaderMatches(ReadOnlySpan<byte> head, string contentType) => contentType switch
    {
        "image/jpeg" => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
        "image/png" => head.Length >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
        "image/webp" => head.Length >= 12 && head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F'
                        && head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P',
        "image/bmp" => head.Length >= 2 && head[0] == (byte)'B' && head[1] == (byte)'M',
        "application/pdf" => head.Length >= 4 && head[0] == (byte)'%' && head[1] == (byte)'P' && head[2] == (byte)'D' && head[3] == (byte)'F',
        _ => false
    };

    private static async Task<bool> LooksLikeAsync(IFormFile file, string contentType)
    {
        var head = new byte[16];
        await using var stream = file.OpenReadStream();
        var read = 0;
        while (read < head.Length)
        {
            var n = await stream.ReadAsync(head.AsMemory(read, head.Length - read));
            if (n == 0) break;
            read += n;
        }
        return HeaderMatches(head.AsSpan(0, read), contentType);
    }

    public ScansController(ApplicationDbContext db, IWebHostEnvironment env, IOptions<StorageOptions> storageOptions) : base(db)
    {
        _env = env;
        _scanStoragePath = DeploymentPaths.ResolveScanStoragePath(storageOptions.Value.ScanStoragePath, env);
    }

    public async Task<IActionResult> Upload(int patientId)
    {
        var patient = await Db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient == null) return NotFound();

        return View(new ScanUploadViewModel
        {
            PatientId = patientId,
            PatientName = patient.FullName,
            ScanDate = DateOnly.FromDateTime(DateTime.Today)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(ScanUploadViewModel vm)
    {
        var patient = await Db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == vm.PatientId);
        if (patient == null) return NotFound();
        vm.PatientName = patient.FullName;

        string? contentType = null;
        if (vm.File == null || vm.File.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.File), "Yüklemek için bir dosya seçin.");
        }
        else
        {
            var ext = Path.GetExtension(vm.File.FileName).ToLowerInvariant();
            if (!AllowedExtensions.TryGetValue(ext, out contentType))
            {
                ModelState.AddModelError(nameof(vm.File), ext is ".dcm" or ".dicom" or ".tif" or ".tiff"
                    ? "DICOM ve TIFF dosyaları doğrudan görüntülenemez. Cihaz yazılımından JPG, PNG veya BMP olarak dışa aktarın."
                    : "Yalnızca JPG, PNG, WebP, BMP ve PDF dosyaları kabul edilir.");
            }
            else if (vm.File.Length > MaxFileBytes)
            {
                ModelState.AddModelError(nameof(vm.File), "Dosya en fazla 20 MB olabilir.");
            }
            else if (!await LooksLikeAsync(vm.File, contentType))
            {
                ModelState.AddModelError(nameof(vm.File), "Dosya içeriği uzantısıyla uyuşmuyor; dosya bozuk veya farklı türde olabilir.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        Directory.CreateDirectory(_scanStoragePath);

        var storedFileName = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}{Path.GetExtension(vm.File!.FileName).ToLowerInvariant()}";
        var filePath = Path.Combine(_scanStoragePath, storedFileName);

        await using (var stream = new FileStream(filePath, FileMode.CreateNew))
        {
            await vm.File.CopyToAsync(stream);
        }

        var scan = new Scan
        {
            PatientId = vm.PatientId,
            FileName = Path.GetFileName(vm.File.FileName),
            StoredPath = storedFileName,
            ContentType = contentType!,
            FileSize = vm.File.Length,
            ScanType = vm.ScanType,
            ScanDate = vm.ScanDate,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Db.Scans.Add(scan);
        await Db.SaveChangesAsync();
        Success("Görüntü yüklendi.");
        return RedirectToAction("Details", "Patients", new { id = scan.PatientId, tab = "scans" });
    }

    public async Task<IActionResult> View(int id)
    {
        var scan = await Db.Scans.AsNoTracking().Include(s => s.Patient).FirstOrDefaultAsync(s => s.Id == id);
        if (scan == null) return NotFound();

        ViewBag.Siblings = await Db.Scans.AsNoTracking()
            .Where(s => s.PatientId == scan.PatientId)
            .OrderByDescending(s => s.ScanDate)
            .ThenByDescending(s => s.Id)
            .Select(s => new { s.Id, s.ScanType, s.ScanDate, s.ContentType, s.FileName })
            .ToListAsync();

        return View(scan);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var scan = await Db.Scans.AsNoTracking().Include(s => s.Patient).FirstOrDefaultAsync(s => s.Id == id);
        if (scan == null) return NotFound();

        return View(new ScanEditViewModel
        {
            Id = scan.Id,
            PatientId = scan.PatientId,
            ScanType = scan.ScanType,
            ScanDate = scan.ScanDate,
            Notes = scan.Notes,
            FileName = scan.FileName,
            PatientName = scan.Patient?.FullName
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, ScanEditViewModel vm)
    {
        if (id != vm.Id) return BadRequest();

        var scan = await Db.Scans.Include(s => s.Patient).FirstOrDefaultAsync(s => s.Id == id);
        if (scan == null) return NotFound();

        if (!ModelState.IsValid)
        {
            vm.FileName = scan.FileName;
            vm.PatientName = scan.Patient?.FullName;
            vm.PatientId = scan.PatientId;
            return View(vm);
        }

        scan.ScanType = vm.ScanType;
        scan.ScanDate = vm.ScanDate;
        scan.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();
        scan.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        Success("Görüntü bilgileri güncellendi.");
        return RedirectToAction(nameof(View), new { id });
    }

    public async Task<IActionResult> ContentFile(int id, bool download = false)
    {
        var scan = await Db.Scans.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (scan == null) return NotFound();

        var filePath = DeploymentPaths.ResolveStoredScanPath(_env, _scanStoragePath, scan.StoredPath);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var downloadName = download ? scan.FileName : null;
        if (!download)
        {
            Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(scan.FileName)}";
        }

        return PhysicalFile(filePath, scan.ContentType, downloadName, enableRangeProcessing: true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AdminOnly]
    public async Task<IActionResult> Delete(int id)
    {
        var scan = await Db.Scans.FindAsync(id);
        if (scan == null) return NotFound();

        var patientId = scan.PatientId;
        var filePath = DeploymentPaths.ResolveStoredScanPath(_env, _scanStoragePath, scan.StoredPath);

        Db.Scans.Remove(scan);
        await Db.SaveChangesAsync();

        if (System.IO.File.Exists(filePath))
        {
            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (IOException)
            {
                // The record is gone; a leftover file is harmless and can be cleaned up manually.
            }
        }

        Success("Görüntü silindi.");
        return RedirectToAction("Details", "Patients", new { id = patientId, tab = "scans" });
    }
}
