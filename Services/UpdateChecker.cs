using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DentistDB.Services;

/// <summary>Where to look for new releases. Bound from the "Updates" configuration section.</summary>
public class UpdateOptions
{
    public const string SectionName = "Updates";

    /// <summary>Turn the check off entirely (tests, development, air-gapped clinics).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>GitHub "owner/repo" whose latest release carries the DentistDB-Setup-*.exe asset.</summary>
    public string Repository { get; set; } = "yigit2403/DentistDB";

    /// <summary>How often the background check runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(12);
}

/// <summary>Result of the last release check. <see cref="IsNewer"/> is the only thing the UI really needs.</summary>
public sealed record UpdateInfo(
    bool Enabled,
    string CurrentVersion,
    string? LatestVersion,
    bool IsNewer,
    string? DownloadUrl,
    string? ReleaseUrl,
    DateTime? CheckedAt,
    string? Error)
{
    public static UpdateInfo Disabled(string current) => new(false, current, null, false, null, null, null, null);
    public static UpdateInfo NotChecked(string current) => new(true, current, null, false, null, null, null, null);
}

public interface IUpdateChecker
{
    /// <summary>Last known result. Returns immediately; never touches the network.</summary>
    UpdateInfo Current { get; }

    /// <summary>Contacts GitHub now (user-initiated "check again").</summary>
    Task<UpdateInfo> RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Looks up the latest GitHub release in the background and remembers the answer so pages can
/// show a "new version available" notice without ever waiting on the network. The clinic PC
/// may be offline; every failure is swallowed into <see cref="UpdateInfo.Error"/>.
/// </summary>
public sealed class UpdateChecker : BackgroundService, IUpdateChecker
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpdateOptions _options;
    private readonly ILogger<UpdateChecker> _logger;
    private readonly string _currentVersion;
    private volatile UpdateInfo _current;

    public UpdateChecker(IHttpClientFactory httpClientFactory, IOptions<UpdateOptions> options, ILogger<UpdateChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _currentVersion = CurrentAssemblyVersion();
        _current = _options.Enabled ? UpdateInfo.NotChecked(_currentVersion) : UpdateInfo.Disabled(_currentVersion);
    }

    public UpdateInfo Current => _current;

    public static string CurrentAssemblyVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        // Let the app finish starting before the first call; then poll on the interval.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);
            try { await Task.Delay(_options.Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    public async Task<UpdateInfo> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return _current;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            var client = _httpClientFactory.CreateClient("github");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{_options.Repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd($"DentistDB/{_currentVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _current = _current with { CheckedAt = DateTime.UtcNow, Error = $"GitHub {(int)response.StatusCode}" };
                return _current;
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            _current = ParseRelease(json, _currentVersion);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Güncelleme kontrolü başarısız.");
            _current = _current with { CheckedAt = DateTime.UtcNow, Error = "Bağlantı kurulamadı" };
        }

        return _current;
    }

    /// <summary>
    /// Pure parsing of the GitHub "latest release" payload, kept static so it can be unit-tested.
    /// Picks the first asset that ends with ".exe" as the installer download.
    /// </summary>
    public static UpdateInfo ParseRelease(string json, string currentVersion)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
        var releaseUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;

        string? download = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    download = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                    break;
                }
            }
        }

        var latest = NormalizeVersion(tag);
        var isNewer = latest is not null
            && Version.TryParse(latest, out var latestV)
            && Version.TryParse(currentVersion, out var currentV)
            && latestV > currentV;

        return new UpdateInfo(true, currentVersion, latest, isNewer, download, releaseUrl, DateTime.UtcNow, latest is null ? "Sürüm okunamadı" : null);
    }

    /// <summary>"v0.5.0" → "0.5.0"; anything unparsable → null.</summary>
    public static string? NormalizeVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var trimmed = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var v) ? v.ToString(Math.Max(3, Math.Min(4, trimmed.Count(c => c == '.') + 1))) : null;
    }
}
