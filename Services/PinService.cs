using System.Security.Cryptography;
using DentistDB.Data;
using DentistDB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DentistDB.Services;

public interface IPinService
{
    Task<bool> VerifyAsync(AppAccountRole role, string? pin, CancellationToken ct = default);
    Task SetPinAsync(AppAccountRole role, string pin, CancellationToken ct = default);
    Task<bool> HasCustomPinAsync(AppAccountRole role, CancellationToken ct = default);
    Task<bool> IsUsingInsecureDefaultsAsync(CancellationToken ct = default);
}

/// <summary>
/// PINs are stored as PBKDF2 hashes in the settings table. When no hash has been saved yet,
/// the configured PIN from appsettings/environment is used so a fresh install can sign in.
/// </summary>
public sealed class PinService : IPinService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly ApplicationDbContext _db;
    private readonly AccessPinOptions _configured;

    public PinService(ApplicationDbContext db, IOptions<AccessPinOptions> configured)
    {
        _db = db;
        _configured = configured.Value;
    }

    public async Task<bool> VerifyAsync(AppAccountRole role, string? pin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return false;
        }

        var stored = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == KeyFor(role), ct);
        if (stored?.Value is { Length: > 0 } hash)
        {
            return VerifyHash(pin, hash);
        }

        var configuredPin = _configured.GetPinFor(role);
        return !string.IsNullOrWhiteSpace(configuredPin)
            && CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(configuredPin),
                System.Text.Encoding.UTF8.GetBytes(pin));
    }

    public async Task SetPinAsync(AppAccountRole role, string pin, CancellationToken ct = default)
    {
        var key = KeyFor(role);
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            setting = new AppSetting { Key = key };
            _db.Settings.Add(setting);
        }

        setting.Value = Hash(pin);
        setting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> HasCustomPinAsync(AppAccountRole role, CancellationToken ct = default)
    {
        var key = KeyFor(role);
        return await _db.Settings.AnyAsync(s => s.Key == key && s.Value != null && s.Value != "", ct);
    }

    public async Task<bool> IsUsingInsecureDefaultsAsync(CancellationToken ct = default)
    {
        foreach (var role in new[] { AppAccountRole.Admin, AppAccountRole.Worker })
        {
            if (await HasCustomPinAsync(role, ct))
            {
                continue;
            }

            if (!AccessPinOptions.IsSecurePin(_configured.GetPinFor(role)))
            {
                return true;
            }
        }

        return false;
    }

    private static string KeyFor(AppAccountRole role) => role == AppAccountRole.Admin
        ? AppSetting.AdminPinHash
        : AppSetting.WorkerPinHash;

    internal static string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    internal static bool VerifyHash(string pin, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
