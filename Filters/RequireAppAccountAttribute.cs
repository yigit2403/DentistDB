using DentistDB.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentistDB.Filters;

public class RequireAppAccountAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.GetCurrentAccount() != null)
        {
            base.OnActionExecuting(context);
            return;
        }

        var request = context.HttpContext.Request;
        var returnUrl = $"{request.Path}{request.QueryString}";

        context.Result = new RedirectToActionResult("Index", "Access", new { returnUrl });
    }
}
