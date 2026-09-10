using DentistDB.Data;
using DentistDB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DentistDB.Services;

public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
    Task<ClinicSettings> GetClinicSettingsAsync(CancellationToken ct = default);
    Task<ClinicIdentity> GetClinicIdentityAsync(CancellationToken ct = default);
}

public sealed record ClinicSettings(string ClinicName, TimeOnly DayStart, TimeOnly DayEnd);

public sealed class SettingsService : ISettingsService
{
    public const string DefaultClinicName = "DentistDB";
    public static readonly TimeOnly DefaultDayStart = new(9, 0);
    public static readonly TimeOnly DefaultDayEnd = new(18, 0);

    private const string CachePrefix = "setting:";
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public SettingsService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CachePrefix + key, out string? cached))
        {
            return cached;
        }

        var value = await _db.Settings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        _cache.Set(CachePrefix + key, value, TimeSpan.FromMinutes(10));
        return value;
    }

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            setting = new AppSetting { Key = key };
            _db.Settings.Add(setting);
        }

        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CachePrefix + key);
    }

    public async Task<ClinicSettings> GetClinicSettingsAsync(CancellationToken ct = default)
    {
        var name = await GetAsync(AppSetting.ClinicName, ct);
        var start = ParseTime(await GetAsync(AppSetting.WorkDayStart, ct)) ?? DefaultDayStart;
        var end = ParseTime(await GetAsync(AppSetting.WorkDayEnd, ct)) ?? DefaultDayEnd;

        if (end <= start)
        {
            start = DefaultDayStart;
            end = DefaultDayEnd;
        }

        return new ClinicSettings(string.IsNullOrWhiteSpace(name) ? DefaultClinicName : name.Trim(), start, end);
    }

    public async Task<ClinicIdentity> GetClinicIdentityAsync(CancellationToken ct = default)
    {
        return new ClinicIdentity(
            (await GetAsync(AppSetting.ClinicDentistName, ct))?.Trim() ?? string.Empty,
            (await GetAsync(AppSetting.ClinicDentistTitle, ct))?.Trim() ?? "Dt.",
            (await GetAsync(AppSetting.ClinicDiplomaNo, ct))?.Trim() ?? string.Empty,
            (await GetAsync(AppSetting.ClinicAddress, ct))?.Trim() ?? string.Empty,
            (await GetAsync(AppSetting.ClinicPhone, ct))?.Trim() ?? string.Empty,
            (await GetAsync(AppSetting.ClinicTaxNo, ct))?.Trim() ?? string.Empty);
    }

    private static TimeOnly? ParseTime(string? value)
    {
        return TimeOnly.TryParseExact(value, "HH:mm", null, System.Globalization.DateTimeStyles.None, out var time)
            ? time
            : null;
    }
}
