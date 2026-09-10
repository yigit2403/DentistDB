using System.Reflection;
using DentistDB.Data;
using DentistDB.Filters;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DentistDB.Controllers;

[AdminOnly]
public class SettingsController : ClinicControllerBase
{
    private readonly ISettingsService _settings;
    private readonly IPinService _pins;
    private readonly BackupService _backup;
    private readonly BackupRunner _backupRunner;
    private readonly IWebHostEnvironment _env;
    private readonly StorageOptions _storage;

    public SettingsController(
        ApplicationDbContext db,
        ISettingsService settings,
        IPinService pins,
        BackupService backup,
        BackupRunner backupRunner,
        IWebHostEnvironment env,
        IOptions<StorageOptions> storage) : base(db)
    {
        _settings = settings;
        _pins = pins;
        _backup = backup;
        _backupRunner = backupRunner;
        _env = env;
        _storage = storage.Value;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildIndexAsync(null, null));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveClinic(ClinicSettingsFormModel clinic)
    {
        if (clinic.DayEnd <= clinic.DayStart)
        {
            ModelState.AddModelError("Clinic.DayEnd", "Çalışma bitişi başlangıçtan sonra olmalıdır.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildIndexAsync(clinic, null));
        }

        await _settings.SetAsync(AppSetting.ClinicName, clinic.ClinicName.Trim());
        await _settings.SetAsync(AppSetting.ClinicDentistTitle, clinic.DentistTitle?.Trim());
        await _settings.SetAsync(AppSetting.ClinicDentistName, clinic.DentistName?.Trim());
        await _settings.SetAsync(AppSetting.ClinicDiplomaNo, clinic.DiplomaNo?.Trim());
        await _settings.SetAsync(AppSetting.ClinicAddress, clinic.Address?.Trim());
        await _settings.SetAsync(AppSetting.ClinicPhone, clinic.Phone?.Trim());
        await _settings.SetAsync(AppSetting.ClinicTaxNo, clinic.TaxNo?.Trim());
        await _settings.SetAsync(AppSetting.WorkDayStart, clinic.DayStart.ToString("HH:mm"));
        await _settings.SetAsync(AppSetting.WorkDayEnd, clinic.DayEnd.ToString("HH:mm"));

        Success("Klinik ayarları kaydedildi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBackup(BackupSettingsFormModel backup)
    {
        backup.Folder = (backup.Folder ?? string.Empty).Trim();
        var folder = Environment.ExpandEnvironmentVariables(backup.Folder);
        if (ModelState.IsValid)
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Backup.Folder", $"Klasör oluşturulamadı: {ex.Message}");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildIndexAsync(null, backup));
        }

        await _settings.SetAsync(AppSetting.BackupFolder, backup.Folder.Trim());
        await _settings.SetAsync(AppSetting.BackupTime, backup.Time.ToString("HH:mm"));
        await _settings.SetAsync(AppSetting.BackupKeepCount, backup.KeepCount.ToString());

        Success("Yedekleme ayarları kaydedildi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RunBackup()
    {
        var result = await _backupRunner.RunAsync();
        if (result.Success)
        {
            Success(result.Message);
        }
        else
        {
            Error(result.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePin(ChangePinViewModel model)
    {
        if (ModelState.IsValid && !await _pins.VerifyAsync(AppAccountRole.Admin, model.CurrentAdminPin))
        {
            ModelState.AddModelError(nameof(model.CurrentAdminPin), "Mevcut yönetici PIN'i hatalı.");
        }

        if (ModelState.IsValid && !AccessPinOptions.IsSecurePin(model.NewPin))
        {
            ModelState.AddModelError(nameof(model.NewPin), "Bu PIN çok zayıf. Tahmin edilmesi zor bir PIN seçin.");
        }

        if (!ModelState.IsValid)
        {
            var vm = await BuildIndexAsync(null, null);
            ViewData["PinRole"] = model.Role;
            return View(nameof(Index), vm);
        }

        await _pins.SetPinAsync(model.Role, model.NewPin);
        Success(model.Role == AppAccountRole.Admin ? "Yönetici PIN'i güncellendi." : "Çalışan PIN'i güncellendi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Backup()
    {
        var clinic = await _settings.GetClinicSettingsAsync();
        var path = await _backup.CreateBackupFileAsync();
        var bytes = await System.IO.File.ReadAllBytesAsync(path);

        try
        {
            System.IO.File.Delete(path);
        }
        catch (IOException)
        {
            // Temp file cleanup is best-effort.
        }

        return File(bytes, "application/vnd.sqlite3", BackupService.SuggestedFileName(clinic.ClinicName));
    }

    // ---- Device connection (LAN, Tailscale, phone calendar) -----------------------

    public async Task<IActionResult> Connect([FromServices] IConnectionInfoService connection)
    {
        var info = await connection.GetAsync();
        var token = await _settings.GetAsync(CalendarController.TokenSettingKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            token = CalendarController.NewToken();
            await _settings.SetAsync(CalendarController.TokenSettingKey, token);
        }

        var clinic = await _settings.GetClinicSettingsAsync();
        return View(new ConnectViewModel
        {
            Info = info,
            ClinicName = clinic.ClinicName,
            CalendarPath = Url.Action("Feed", "Calendar", new { token })!,
            IsProduction = !_env.IsDevelopment()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateCalendarToken()
    {
        await _settings.SetAsync(CalendarController.TokenSettingKey, CalendarController.NewToken());
        Success("Takvim bağlantısı yenilendi. Telefondaki eski abonelik artık çalışmaz; yeni adresi tekrar ekleyin.");
        return RedirectToAction(nameof(Connect));
    }

    // ---- Consent templates ----------------------------------------------------

    public async Task<IActionResult> Consents()
    {
        return View(new ConsentTemplatesFormModel
        {
            TreatmentText = await _settings.GetAsync(AppSetting.ConsentTreatmentText) ?? ConsentTemplates.TreatmentDefault.Trim(),
            KvkkText = await _settings.GetAsync(AppSetting.ConsentKvkkText) ?? ConsentTemplates.KvkkDefault.Trim()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Consents(ConsentTemplatesFormModel model, string? reset)
    {
        if (reset == "treatment")
        {
            await _settings.SetAsync(AppSetting.ConsentTreatmentText, null);
            Success("Tedavi onam metni varsayılana döndürüldü.");
            return RedirectToAction(nameof(Consents));
        }

        if (reset == "kvkk")
        {
            await _settings.SetAsync(AppSetting.ConsentKvkkText, null);
            Success("KVKK metni varsayılana döndürüldü.");
            return RedirectToAction(nameof(Consents));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _settings.SetAsync(AppSetting.ConsentTreatmentText, model.TreatmentText.Trim());
        await _settings.SetAsync(AppSetting.ConsentKvkkText, model.KvkkText.Trim());
        Success("Form metinleri kaydedildi.");
        return RedirectToAction(nameof(Consents));
    }

    // ---- Audit log ---------------------------------------------------------------

    public async Task<IActionResult> AuditLog(string? search, string? account, string? entityType, DateOnly? from, DateOnly? to, int pageNumber = 1)
    {
        var query = Db.AuditEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(account))
        {
            query = query.Where(a => a.Account == account);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            query = query.Where(a => a.At >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            query = query.Where(a => a.At < toUtc);
        }

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(a => EF.Functions.Like(a.Summary, pattern) || (a.Details != null && EF.Functions.Like(a.Details, pattern)));
        }

        var vm = new AuditLogViewModel
        {
            Search = term,
            Account = account,
            EntityType = entityType,
            From = from,
            To = to,
            Entries = await PaginatedList<AuditEntry>.CreateAsync(query.OrderByDescending(a => a.At).ThenByDescending(a => a.Id), pageNumber, 50),
            EntityTypes = await Db.AuditEntries.AsNoTracking().Select(a => a.EntityType).Distinct().OrderBy(t => t).ToListAsync()
        };

        return View(vm);
    }

    // ---- Price list --------------------------------------------------------------

    public async Task<IActionResult> PriceList(bool showInactive = false)
    {
        var query = Db.Procedures.AsNoTracking().AsQueryable();
        if (!showInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return View(new PriceListViewModel
        {
            ShowInactive = showInactive,
            Procedures = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProcedure(ProcedureFormViewModel model, bool showInactive = false)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (ModelState.IsValid)
        {
            var normalized = SearchNormalizer.Normalize(model.Name);
            var others = await Db.Procedures.Where(p => p.Id != model.Id).Select(p => p.Name).ToListAsync();
            if (others.Any(n => SearchNormalizer.Normalize(n) == normalized))
            {
                ModelState.AddModelError("NewProcedure.Name", "Bu isimde bir işlem zaten var.");
            }
        }

        if (!ModelState.IsValid)
        {
            var query = Db.Procedures.AsNoTracking().AsQueryable();
            if (!showInactive) query = query.Where(p => p.IsActive);
            return View(nameof(PriceList), new PriceListViewModel
            {
                ShowInactive = showInactive,
                NewProcedure = model,
                Procedures = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync()
            });
        }

        if (model.Id > 0)
        {
            var existing = await Db.Procedures.FindAsync(model.Id);
            if (existing == null) return NotFound();
            existing.Name = model.Name;
            existing.DefaultPrice = Math.Round(model.DefaultPrice, 2, MidpointRounding.AwayFromZero);
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            Success("İşlem güncellendi.");
        }
        else
        {
            var maxOrder = await Db.Procedures.MaxAsync(p => (int?)p.SortOrder) ?? -1;
            Db.Procedures.Add(new Procedure
            {
                Name = model.Name,
                DefaultPrice = Math.Round(model.DefaultPrice, 2, MidpointRounding.AwayFromZero),
                IsActive = true,
                SortOrder = maxOrder + 1
            });
            Success("İşlem fiyat listesine eklendi.");
        }

        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(PriceList), new { showInactive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProcedure(int id, bool showInactive = false)
    {
        var procedure = await Db.Procedures.FindAsync(id);
        if (procedure == null) return NotFound();

        procedure.IsActive = !procedure.IsActive;
        procedure.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        Success(procedure.IsActive ? $"{procedure.Name} aktif edildi." : $"{procedure.Name} pasife alındı.");
        return RedirectToAction(nameof(PriceList), new { showInactive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProcedure(int id, bool showInactive = false)
    {
        var procedure = await Db.Procedures.FindAsync(id);
        if (procedure == null) return NotFound();

        var inUse = await Db.InvoiceItems.AnyAsync(i => i.ProcedureId == id) || await Db.TreatmentPlanItems.AnyAsync(t => t.ProcedureId == id);
        if (inUse)
        {
            procedure.IsActive = false;
            procedure.UpdatedAt = DateTime.UtcNow;
            Warning($"{procedure.Name} kullanıldığı için silinmedi, pasife alındı.");
        }
        else
        {
            Db.Procedures.Remove(procedure);
            Success($"{procedure.Name} silindi.");
        }

        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(PriceList), new { showInactive });
    }

    private async Task<SettingsIndexViewModel> BuildIndexAsync(ClinicSettingsFormModel? clinicOverride, BackupSettingsFormModel? backupOverride)
    {
        var clinic = await _settings.GetClinicSettingsAsync();
        var identity = await _settings.GetClinicIdentityAsync();
        var backup = await _backupRunner.GetSettingsAsync();
        var connection = new SqliteConnectionStringBuilder(Db.Database.GetConnectionString());
        var dbPath = connection.DataSource;
        long size = 0;
        if (System.IO.File.Exists(dbPath))
        {
            size = new FileInfo(dbPath).Length;
        }

        return new SettingsIndexViewModel
        {
            Clinic = clinicOverride ?? new ClinicSettingsFormModel
            {
                ClinicName = clinic.ClinicName,
                DentistTitle = identity.DentistTitle,
                DentistName = identity.DentistName,
                DiplomaNo = identity.DiplomaNo,
                Address = identity.Address,
                Phone = identity.Phone,
                TaxNo = identity.TaxNo,
                DayStart = clinic.DayStart,
                DayEnd = clinic.DayEnd
            },
            Backup = backupOverride ?? new BackupSettingsFormModel
            {
                Folder = backup.Folder,
                Time = backup.Time,
                KeepCount = backup.KeepCount
            },
            BackupStatus = backup,
            AdminPinIsCustom = await _pins.HasCustomPinAsync(AppAccountRole.Admin),
            WorkerPinIsCustom = await _pins.HasCustomPinAsync(AppAccountRole.Worker),
            ActiveProcedureCount = await Db.Procedures.CountAsync(p => p.IsActive),
            DatabasePath = dbPath,
            DatabaseSizeBytes = size,
            ScanStoragePath = DeploymentPaths.ResolveScanStoragePath(_storage.ScanStoragePath, _env),
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.4.0"
        };
    }
}
