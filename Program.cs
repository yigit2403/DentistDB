using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using DentistDB.Data;
using DentistDB.Infrastructure;
using DentistDB.Models;
using DentistDB.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services
    .AddOptions<AccessPinOptions>()
    .Bind(builder.Configuration.GetSection(AccessPinOptions.SectionName));

builder.Services
    .AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName));

var sqliteConnectionString = DeploymentPaths.ResolveSqliteConnectionString(builder.Configuration, builder.Environment);
var scanStoragePath = DeploymentPaths.ResolveScanStoragePath(builder.Configuration, builder.Environment);
var dataProtectionKeysPath = DeploymentPaths.ResolveDataProtectionKeysPath(builder.Configuration, builder.Environment);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAccountAccessor, CurrentAccountAccessor>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlite(sqliteConnectionString)
           .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

builder.Services
    .AddDataProtection()
    .SetApplicationName("DentistDB")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddScoped<IPinService, PinService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<BackupRunner>();
builder.Services.AddSingleton<IConnectionInfoService, ConnectionInfoService>();
builder.Services.AddHostedService<NightlyBackupService>();

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider());

    var provider = options.ModelBindingMessageProvider;
    provider.SetValueIsInvalidAccessor(_ => "Geçersiz bir değer girdiniz.");
    provider.SetValueMustBeANumberAccessor(_ => "Bu alan sayısal olmalıdır.");
    provider.SetMissingBindRequiredValueAccessor(_ => "Bu alan zorunludur.");
    provider.SetAttemptedValueIsInvalidAccessor((value, fieldName) => $"'{value}' değeri {fieldName} alanı için geçerli değil.");
    provider.SetMissingKeyOrValueAccessor(() => "Bu alan zorunludur.");
    provider.SetUnknownValueIsInvalidAccessor(_ => "Geçersiz bir seçim yaptınız.");
    provider.SetValueMustNotBeNullAccessor(_ => "Bu alan zorunludur.");
    provider.SetNonPropertyValueMustBeANumberAccessor(() => "Bu alan sayısal olmalıdır.");
    provider.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"'{value}' geçerli bir değer değil.");
});

// Emit Turkish characters as-is instead of numeric entities: smaller pages, readable HTML.
builder.Services.Configure<WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024;
});

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".DentistDB.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.IdleTimeout = TimeSpan.FromHours(12);
});

var app = builder.Build();

var turkish = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkish;
CultureInfo.DefaultThreadCurrentUICulture = turkish;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkish),
    SupportedCultures = new[] { turkish },
    SupportedUICultures = new[] { turkish }
});

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await db.Database.MigrateAsync();
    await SeedData.SeedReferenceDataAsync(db);

    if (app.Environment.IsDevelopment())
    {
        await SeedData.SeedDemoDataAsync(db);
    }
    else
    {
        var pins = scope.ServiceProvider.GetRequiredService<IPinService>();
        if (await pins.IsUsingInsecureDefaultsAsync())
        {
            logger.LogCritical("Production start refused: configure non-default AccessPins (Admin/Worker) or set PINs from the settings page before deploying.");
            throw new InvalidOperationException("Configure non-default access PINs for production (AccessPins:Admin / AccessPins:Worker).");
        }
    }
}

Directory.CreateDirectory(scanStoragePath);

// tailscale serve (or any local reverse proxy) terminates HTTPS and forwards to Kestrel over loopback.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");

    // Only force HTTPS when Kestrel itself has an HTTPS endpoint; LAN clients talk plain HTTP
    // to the clinic PC while Tailscale provides HTTPS from outside.
    var listensOnHttps = (builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["urls"] ?? string.Empty)
        .Contains("https://", StringComparison.OrdinalIgnoreCase)
        || builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren().Any(e => (e["Url"] ?? string.Empty).StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    if (listensOnHttps)
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Exposed so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program { }
