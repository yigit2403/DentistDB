using DentistDB.Data;
using DentistDB.Extensions;
using DentistDB.Models;
using DentistDB.Services;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DentistDB.Controllers;

public class AccessController : Controller
{
    private readonly IPinService _pins;
    private readonly LoginThrottle _throttle;
    private readonly ISettingsService _settings;
    private readonly ApplicationDbContext _db;

    public AccessController(IPinService pins, LoginThrottle throttle, ISettingsService settings, ApplicationDbContext db)
    {
        _pins = pins;
        _throttle = throttle;
        _settings = settings;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl = null)
    {
        if (HttpContext.GetCurrentAccount() != null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        return View(await BuildModelAsync(new AccessSelectionViewModel { ReturnUrl = returnUrl }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(AccessSelectionViewModel model)
    {
        var clientKey = HttpContext.GetClientKey();
        var account = AppAccounts.Find(model.AccountKey);

        if (account == null)
        {
            ModelState.AddModelError(nameof(model.AccountKey), "Geçersiz hesap seçimi.");
        }

        if (_throttle.GetRemainingLockout(clientKey) is { } remaining)
        {
            ModelState.AddModelError(nameof(model.Pin), $"Çok fazla hatalı deneme. {Math.Ceiling(remaining.TotalSeconds)} saniye sonra tekrar deneyin.");
            model.LockoutSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            return View(nameof(Index), await BuildModelAsync(model));
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildModelAsync(model));
        }

        if (!await _pins.VerifyAsync(account!.Role, model.Pin))
        {
            _throttle.RegisterFailure(clientKey);
            _db.AuditEntries.Add(AuditService.LoginEvent(account.Key, success: false, clientKey));
            await _db.SaveChangesAsync();

            ModelState.AddModelError(nameof(model.Pin), "Girilen PIN hatalı.");
            model.Pin = string.Empty;
            return View(nameof(Index), await BuildModelAsync(model));
        }

        _throttle.RegisterSuccess(clientKey);
        HttpContext.SignInAccount(account.Key);
        _db.AuditEntries.Add(AuditService.LoginEvent(account.Key, success: true, clientKey));
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var account = HttpContext.GetCurrentAccount();
        if (account != null)
        {
            _db.AuditEntries.Add(AuditService.LogoutEvent(account.Key));
            await _db.SaveChangesAsync();
        }

        HttpContext.SignOutAccount();
        return RedirectToAction(nameof(Index));
    }

    private async Task<AccessSelectionViewModel> BuildModelAsync(AccessSelectionViewModel model)
    {
        var clinic = await _settings.GetClinicSettingsAsync();
        model.Accounts = AppAccounts.All;
        model.ClinicName = clinic.ClinicName;
        if (AppAccounts.Find(model.AccountKey) is null)
        {
            model.AccountKey = AppAccounts.Admin.Key;
        }
        return model;
    }
}
