using DentistDB.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentistDB.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var httpContext = context.HttpContext;
        var account = httpContext.GetCurrentAccount();

        if (account == null)
        {
            var request = httpContext.Request;
            var returnUrl = HttpMethods.IsGet(request.Method) ? $"{request.Path}{request.QueryString}" : null;
            context.Result = new RedirectToActionResult("Index", "Access", new { returnUrl });
            return;
        }

        if (httpContext.IsAdmin())
        {
            base.OnActionExecuting(context);
            return;
        }

        if (context.Controller is Controller controller)
        {
            controller.TempData["Error"] = "Bu işlem yalnızca yönetici hesabıyla yapılabilir.";
        }

        var referer = httpContext.Request.Headers.Referer.ToString();
        context.Result = !string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
            && string.Equals(refererUri.Host, httpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            ? new RedirectResult(refererUri.PathAndQuery)
            : new RedirectToActionResult("Index", "Home", null);
    }
}
