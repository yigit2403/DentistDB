using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using DentistDB.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DentistDB.Tests.Integration;

/// <summary>
/// Hosts the real application against a throw-away SQLite database in the temp folder.
/// The Development environment is used so the demo seed and the default dev PINs (1234 / 5678) apply.
/// </summary>
public sealed class ClinicAppFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DentistDB.Tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_root);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(_root, "test.db")}");
        builder.UseSetting("Storage:ScanStoragePath", Path.Combine(_root, "scans"));
        builder.UseSetting("Storage:DataProtectionKeysPath", Path.Combine(_root, "keys"));
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command", "Warning");
    }

    public HttpClient CreateBrowser()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    public T WithDb<T>(Func<ApplicationDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return query(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp database.
        }
    }
}

public static class BrowserExtensions
{
    private static readonly Regex TokenRegex = new("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.Compiled);

    public static async Task<string> GetTokenAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = TokenRegex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException($"No antiforgery token found on {url}.");
        }

        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string url, string tokenPageUrl, params (string Key, string Value)[] fields)
    {
        var token = await client.GetTokenAsync(tokenPageUrl);
        var content = new FormUrlEncodedContent(fields
            .Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
            .Append(new KeyValuePair<string, string>("__RequestVerificationToken", token)));

        return await client.PostAsync(url, content);
    }

    public static async Task LoginAsync(this HttpClient client, string accountKey, string pin)
    {
        var response = await client.PostFormAsync("/Access/Select", "/Access",
            ("AccountKey", accountKey),
            ("Pin", pin));

        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Login failed with {response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
        }
    }

    /// <summary>Reads the body and decodes HTML entities so assertions can use plain Turkish text.</summary>
    public static async Task<string> ReadAsync(this HttpResponseMessage response)
    {
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
}
