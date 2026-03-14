using DentistDB.Extensions;
using DentistDB.Models;
using Microsoft.AspNetCore.Mvc;

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

        ViewBag.ReturnUrl = returnUrl;
        return View(AppAccounts.All);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Select(string accountKey, string? returnUrl = null)
    {
        var account = AppAccounts.Find(accountKey);
        if (account == null)
        {
            TempData["Error"] = "Geçersiz hesap seçimi.";
            return RedirectToAction(nameof(Index));
        }

        HttpContext.SignInAccount(account.Key);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
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
