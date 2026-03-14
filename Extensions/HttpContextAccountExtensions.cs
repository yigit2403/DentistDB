using DentistDB.Models;

namespace DentistDB.Extensions;

public static class HttpContextAccountExtensions
{
    public static AppAccount? GetCurrentAccount(this HttpContext httpContext)
    {
        var accountKey = httpContext.Session.GetString(AppAccounts.SessionKey);
        return AppAccounts.Find(accountKey);
    }

    public static bool CanViewFinancials(this HttpContext httpContext)
    {
        return httpContext.GetCurrentAccount()?.Role == AppAccountRole.Admin;
    }

    public static void SignInAccount(this HttpContext httpContext, string accountKey)
    {
        var account = AppAccounts.Find(accountKey)
            ?? throw new InvalidOperationException($"Unknown account key '{accountKey}'.");

        httpContext.Session.SetString(AppAccounts.SessionKey, account.Key);
    }

    public static void SignOutAccount(this HttpContext httpContext)
    {
        httpContext.Session.Remove(AppAccounts.SessionKey);
    }
}
