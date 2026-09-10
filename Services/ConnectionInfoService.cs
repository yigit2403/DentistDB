using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace DentistDB.Services;

/// <summary>Everything the "Cihaz Bağlantısı" page needs to show how to reach this server.</summary>
public sealed record ServerConnectionInfo(
    IReadOnlyList<string> LanUrls,
    string? TailscaleUrl,
    string? TailscaleHostName,
    IReadOnlyList<string> TailscaleIps,
    bool TailscaleInstalled,
    bool TailscaleOnline,
    string? TailscaleError,
    int Port);

public interface IConnectionInfoService
{
    Task<ServerConnectionInfo> GetAsync(CancellationToken ct = default);
}

public sealed class ConnectionInfoService : IConnectionInfoService
{
    private const string CacheKey = "connection-info";
    private readonly IMemoryCache _cache;
    private readonly Microsoft.AspNetCore.Hosting.Server.IServer _server;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectionInfoService> _logger;

    public ConnectionInfoService(IMemoryCache cache, Microsoft.AspNetCore.Hosting.Server.IServer server, IConfiguration configuration, ILogger<ConnectionInfoService> logger)
    {
        _cache = cache;
        _server = server;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ServerConnectionInfo> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out ServerConnectionInfo? cached) && cached != null)
        {
            return cached;
        }

        var port = ResolvePort();
        var lanUrls = GetLanAddresses().Select(ip => $"http://{ip}:{port}").ToList();
        var machineName = Environment.MachineName.ToLowerInvariant();
        lanUrls.Insert(0, $"http://{machineName}:{port}");

        var tailscale = await ReadTailscaleAsync(ct);

        var info = new ServerConnectionInfo(
            lanUrls,
            tailscale.Url,
            tailscale.HostName,
            tailscale.Ips,
            tailscale.Installed,
            tailscale.Online,
            tailscale.Error,
            port);

        _cache.Set(CacheKey, info, TimeSpan.FromSeconds(30));
        return info;
    }

    private int ResolvePort()
    {
        var addresses = _server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses ?? Array.Empty<string>();
        foreach (var address in addresses)
        {
            if (Uri.TryCreate(address.Replace("*", "localhost").Replace("+", "localhost").Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var uri))
            {
                return uri.Port;
            }
        }

        var urls = _configuration["ASPNETCORE_URLS"] ?? _configuration["urls"];
        if (!string.IsNullOrWhiteSpace(urls))
        {
            var first = urls.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (first != null && Uri.TryCreate(first.Replace("*", "localhost").Replace("+", "localhost").Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var uri))
            {
                return uri.Port;
            }
        }

        return 5000;
    }

    private static IEnumerable<string> GetLanAddresses()
    {
        var result = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                if (nic.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) || nic.Name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)) continue;
                if (nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) || nic.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) || nic.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var text = unicast.Address.ToString();
                    if (text.StartsWith("169.254.")) continue;
                    result.Add(text);
                }
            }
        }
        catch (Exception)
        {
            // Network enumeration is best-effort.
        }

        return result.Distinct();
    }

    private async Task<(bool Installed, bool Online, string? Url, string? HostName, IReadOnlyList<string> Ips, string? Error)> ReadTailscaleAsync(CancellationToken ct)
    {
        var exe = FindTailscaleExe();
        if (exe == null)
        {
            return (false, false, null, null, Array.Empty<string>(), null);
        }

        try
        {
            var psi = new ProcessStartInfo(exe, "status --json")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (true, false, null, null, Array.Empty<string>(), "Tailscale başlatılamadı.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            return ParseStatus(output);
        }
        catch (OperationCanceledException)
        {
            return (true, false, null, null, Array.Empty<string>(), "Tailscale yanıt vermedi.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tailscale status failed");
            return (true, false, null, null, Array.Empty<string>(), ex.Message);
        }
    }

    /// <summary>Parses `tailscale status --json`. Public for tests.</summary>
    public static (bool Installed, bool Online, string? Url, string? HostName, IReadOnlyList<string> Ips, string? Error) ParseStatus(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var backendState = root.TryGetProperty("BackendState", out var bs) ? bs.GetString() : null;
            var online = string.Equals(backendState, "Running", StringComparison.OrdinalIgnoreCase);

            string? dnsName = null;
            var ips = new List<string>();
            if (root.TryGetProperty("Self", out var self))
            {
                if (self.TryGetProperty("DNSName", out var dns)) dnsName = dns.GetString()?.TrimEnd('.');
                if (self.TryGetProperty("TailscaleIPs", out var ipArray) && ipArray.ValueKind == JsonValueKind.Array)
                {
                    ips.AddRange(ipArray.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s))!);
                }
            }

            var url = string.IsNullOrWhiteSpace(dnsName) ? null : $"https://{dnsName}";
            var error = online ? null : backendState switch
            {
                "NeedsLogin" => "Tailscale oturumu açılmamış. Klinik bilgisayarında Tailscale'e giriş yapın.",
                "Stopped" => "Tailscale durdurulmuş. Tailscale simgesinden bağlantıyı açın.",
                _ => $"Tailscale durumu: {backendState ?? "bilinmiyor"}"
            };

            return (true, online, url, dnsName, ips, error);
        }
        catch (JsonException)
        {
            return (true, false, null, null, Array.Empty<string>(), "Tailscale durumu okunamadı.");
        }
    }

    private static string? FindTailscaleExe()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tailscale", "tailscale.exe"),
            "/usr/bin/tailscale",
            "/Applications/Tailscale.app/Contents/MacOS/Tailscale"
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
