using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Models;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services
    .AddOptions<AccessPinOptions>()
    .Bind(builder.Configuration.GetSection(AccessPinOptions.SectionName))
    .Validate(
        options => builder.Environment.IsDevelopment() || options.UsesSecurePins(),
        "Configure non-default access pins for production.")
    .ValidateOnStart();

builder.Services
    .AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName));

var sqliteConnectionString = DeploymentPaths.ResolveSqliteConnectionString(builder.Configuration, builder.Environment);
var scanStoragePath = DeploymentPaths.ResolveScanStoragePath(builder.Configuration, builder.Environment);
var dataProtectionKeysPath = DeploymentPaths.ResolveDataProtectionKeysPath(builder.Configuration, builder.Environment);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.AddControllersWithViews();
builder.Services.AddLocalization();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(12);
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    var provider = options.ModelBindingMessageProvider;
    provider.SetValueIsInvalidAccessor(_ => "Geçersiz bir değer girdiniz.");
    provider.SetValueMustBeANumberAccessor(_ => "Bu alan sayısal olmalıdır.");
    provider.SetMissingBindRequiredValueAccessor(_ => "Bu alan zorunludur.");
    provider.SetAttemptedValueIsInvalidAccessor((value, fieldName) => $"{fieldName} alanına girilen '{value}' değeri geçerli değildir.");
    provider.SetMissingKeyOrValueAccessor(() => "Bu alan zorunludur.");
    provider.SetUnknownValueIsInvalidAccessor(_ => "Geçersiz bir seçim yaptınız.");
});

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("tr-TR") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("tr-TR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        await DatabaseSchemaInitializer.EnsureAsync(db);
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

Directory.CreateDirectory(scanStoragePath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Access}/{action=Index}/{id?}");

app.Run();
