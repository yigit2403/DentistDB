using DentistDB.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentistDB.Filters;

public class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var httpContext = context.HttpContext;
        var account = httpContext.GetCurrentAccount();

        if (account == null)
        {
            var request = httpContext.Request;
            var returnUrl = $"{request.Path}{request.QueryString}";
            context.Result = new RedirectToActionResult("Index", "Access", new { returnUrl });
            return;
        }

        if (httpContext.CanViewFinancials())
        {
            base.OnActionExecuting(context);
            return;
        }

        if (context.Controller is Controller controller)
        {
            controller.TempData["Error"] = "Bu alan yalnızca yönetici hesabı için kullanılabilir.";
        }

        context.Result = new RedirectToActionResult("Index", "Home", null);
    }
}
