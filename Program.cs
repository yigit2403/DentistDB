using Microsoft.EntityFrameworkCore;
using DentistDB.Data;
using DentistDB.Models;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AccessPinOptions>(
    builder.Configuration.GetSection(AccessPinOptions.SectionName));

// Database – SQLite in development, MySQL in production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite("Data Source=DentistDB_dev.db"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21))));
}

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

// Seed data
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (app.Environment.IsDevelopment())
        {
            // SQLite: use EnsureCreated (bypasses MySQL-specific migrations)
            db.Database.EnsureCreated();
        }
        else
        {
            // MySQL production: apply EF migrations
            db.Database.Migrate();
        }
        await DatabaseSchemaInitializer.EnsureAsync(db);
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Ensure scan upload directory exists
var scanPath = Path.Combine(builder.Environment.WebRootPath, "uploads", "scans");
Directory.CreateDirectory(scanPath);

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

