using DentistDB.Data;
using DentistDB.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Controllers;

[RequireAppAccount]
public abstract class ClinicControllerBase : Controller
{
    protected readonly ApplicationDbContext Db;

    protected ClinicControllerBase(ApplicationDbContext db)
    {
        Db = db;
    }

    protected async Task<List<SelectListItem>> GetPatientSelectListAsync(int? includePatientId = null)
    {
        var query = Db.Patients.AsNoTracking().Where(p => !p.IsArchived || p.Id == includePatientId);

        return await query
            .OrderBy(p => p.FullName)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Phone == null ? p.FullName : p.FullName + " · " + p.Phone
            })
            .ToListAsync();
    }

    protected string? SafeReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
    }

    protected IActionResult RedirectToReturnUrlOr(string? returnUrl, IActionResult fallback)
    {
        var safe = SafeReturnUrl(returnUrl);
        return safe is null ? fallback : Redirect(safe);
    }

    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;
    protected void Warning(string message) => TempData["Warning"] = message;
}
