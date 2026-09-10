using System.Collections.Concurrent;

namespace DentistDB.Services;

/// <summary>
/// In-memory brute-force protection for the PIN screen: after a handful of failures from one
/// address, further attempts are refused for a cooling-off period.
/// </summary>
public sealed class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public TimeSpan? GetRemainingLockout(string clientKey)
    {
        if (!_entries.TryGetValue(clientKey, out var entry))
        {
            return null;
        }

        if (entry.LockedUntil is { } until && until > DateTimeOffset.UtcNow)
        {
            return until - DateTimeOffset.UtcNow;
        }

        return null;
    }

    public void RegisterFailure(string clientKey)
    {
        var now = DateTimeOffset.UtcNow;
        _entries.AddOrUpdate(
            clientKey,
            _ => new Entry(1, now, null),
            (_, existing) =>
            {
                var failures = now - existing.FirstFailure > Window ? 1 : existing.Failures + 1;
                var firstFailure = failures == 1 ? now : existing.FirstFailure;
                var lockedUntil = failures >= MaxFailures ? now + Lockout : existing.LockedUntil;
                return new Entry(failures, firstFailure, lockedUntil);
            });
    }

    public void RegisterSuccess(string clientKey)
    {
        _entries.TryRemove(clientKey, out _);
    }

    private sealed record Entry(int Failures, DateTimeOffset FirstFailure, DateTimeOffset? LockedUntil);
}
