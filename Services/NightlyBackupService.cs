using DentistDB.Data;
using DentistDB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DentistDB.Services;

/// <summary>Result of one backup run, also persisted to settings for the status indicator.</summary>
public sealed record BackupRunResult(bool Success, string Message, string? FilePath, int CopiedScanFiles);

public sealed record BackupSettings(string Folder, TimeOnly Time, int KeepCount, DateTime? LastRunAt, string? LastResult)
{
    public bool IsStale => LastRunAt is null || DateTime.UtcNow - LastRunAt > TimeSpan.FromHours(36);
    public bool LastFailed => LastResult != null && LastResult.StartsWith("HATA", StringComparison.Ordinal);
}

/// <summary>Creates dated database backups and mirrors new scan files into the backup folder.</summary>
public sealed class BackupRunner
{
    private readonly ApplicationDbContext _db;
    private readonly BackupService _backup;
    private readonly ISettingsService _settings;
    private readonly IWebHostEnvironment _env;
    private readonly StorageOptions _storage;
    private readonly ILogger<BackupRunner> _logger;

    public BackupRunner(ApplicationDbContext db, BackupService backup, ISettingsService settings, IWebHostEnvironment env, IOptions<StorageOptions> storage, ILogger<BackupRunner> logger)
    {
        _db = db;
        _backup = backup;
        _settings = settings;
        _env = env;
        _storage = storage.Value;
        _logger = logger;
    }

    public async Task<BackupSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var folder = await _settings.GetAsync(AppSetting.BackupFolder, ct);
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = _env.IsDevelopment()
                ? Path.Combine(_env.ContentRootPath, "backups")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DentistDB", "backups");
        }

        var time = TimeOnly.TryParseExact(await _settings.GetAsync(AppSetting.BackupTime, ct), "HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : new TimeOnly(13, 0);
        var keep = int.TryParse(await _settings.GetAsync(AppSetting.BackupKeepCount, ct), out var k) && k > 0 ? k : 30;
        DateTime? lastRun = DateTime.TryParse(await _settings.GetAsync(AppSetting.BackupLastRunAt, ct), null, System.Globalization.DateTimeStyles.RoundtripKind, out var lr) ? lr : null;
        var lastResult = await _settings.GetAsync(AppSetting.BackupLastResult, ct);

        return new BackupSettings(Environment.ExpandEnvironmentVariables(folder), time, keep, lastRun, lastResult);
    }

    public async Task<BackupRunResult> RunAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        BackupRunResult result;

        try
        {
            Directory.CreateDirectory(settings.Folder);

            var clinic = await _settings.GetClinicSettingsAsync(ct);
            var tempFile = await _backup.CreateBackupFileAsync(ct);
            var target = Path.Combine(settings.Folder, BackupService.SuggestedFileName(clinic.ClinicName));
            File.Move(tempFile, target, overwrite: true);

            var copied = MirrorScans(Path.Combine(settings.Folder, "scans"));
            Prune(settings.Folder, settings.KeepCount);

            result = new BackupRunResult(true, $"Yedek alındı: {Path.GetFileName(target)} ({copied} yeni görüntü dosyası kopyalandı)", target, copied);
            _logger.LogInformation("Backup written to {Path}", target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            result = new BackupRunResult(false, $"HATA: {ex.Message}", null, 0);
        }

        await _settings.SetAsync(AppSetting.BackupLastRunAt, DateTime.UtcNow.ToString("O"), ct);
        await _settings.SetAsync(AppSetting.BackupLastResult, result.Message, ct);
        return result;
    }

    private int MirrorScans(string targetFolder)
    {
        var source = DeploymentPaths.ResolveScanStoragePath(_storage.ScanStoragePath, _env);
        if (!Directory.Exists(source))
        {
            return 0;
        }

        Directory.CreateDirectory(targetFolder);
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var destination = Path.Combine(targetFolder, Path.GetFileName(file));
            var sourceInfo = new FileInfo(file);
            var destInfo = new FileInfo(destination);
            if (!destInfo.Exists || destInfo.Length != sourceInfo.Length || destInfo.LastWriteTimeUtc < sourceInfo.LastWriteTimeUtc)
            {
                File.Copy(file, destination, overwrite: true);
                copied++;
            }
        }

        return copied;
    }

    private static void Prune(string folder, int keepCount)
    {
        var backups = new DirectoryInfo(folder).GetFiles("*.db")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(keepCount)
            .ToList();

        foreach (var old in backups)
        {
            try { old.Delete(); } catch (IOException) { /* keep going */ }
        }
    }
}

/// <summary>Runs the backup once a day at the configured time.</summary>
public sealed class NightlyBackupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NightlyBackupService> _logger;

    public NightlyBackupService(IServiceScopeFactory scopeFactory, ILogger<NightlyBackupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the web host finish starting before touching the database.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<BackupRunner>();
                var settings = await runner.GetSettingsAsync(stoppingToken);

                var now = DateTime.Now;
                var nextRun = now.Date.Add(settings.Time.ToTimeSpan());
                if (nextRun <= now) nextRun = nextRun.AddDays(1);

                // Catch up immediately if the machine was off at the scheduled time.
                var missed = settings.LastRunAt is null || settings.LastRunAt.Value.ToLocalTime() < now.Date.Add(settings.Time.ToTimeSpan()).AddDays(-1);
                delay = missed && (settings.LastRunAt is null || DateTime.UtcNow - settings.LastRunAt > TimeSpan.FromHours(24)) ? TimeSpan.FromSeconds(5) : nextRun - now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup scheduler could not read settings");
                delay = TimeSpan.FromHours(1);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<BackupRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup failed");
            }

            // Avoid double-running within the same minute.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
