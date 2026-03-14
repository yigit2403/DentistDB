using DentistDB.Extensions;
using DentistDB.Models;
using DentistDB.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DentistDB.Controllers;

public class AccessController : Controller
{
    [HttpGet]
    public IActionResult Index(string? returnUrl = null)
    {
        if (HttpContext.GetCurrentAccount() != null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        return View(new AccessSelectionViewModel
        {
            Accounts = AppAccounts.All,
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Select(
        AccessSelectionViewModel model,
        [FromServices] IOptions<AccessPinOptions> pinOptions)
    {
        model.Accounts = AppAccounts.All;

        var account = AppAccounts.Find(model.AccountKey);
        if (account == null)
        {
            TempData["Error"] = "Geçersiz hesap seçimi.";
            return RedirectToAction(nameof(Index), new { returnUrl = model.ReturnUrl });
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        var expectedPin = pinOptions.Value.GetPinFor(account.Key);
        if (!string.Equals(model.Pin, expectedPin, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Pin), "Girilen PIN hatalı.");
            return View(nameof(Index), model);
        }

        HttpContext.SignInAccount(account.Key);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.SignOutAccount();
        return RedirectToAction(nameof(Index));
    }
}
