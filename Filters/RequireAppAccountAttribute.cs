using DentistDB.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentistDB.Filters;

/// <summary>Requires a signed-in PIN account. Actions marked with <see cref="AllowAnonymousAccountAttribute"/> are exempt.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAppAccountAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var allowsAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAccountAttribute>().Any();
        if (allowsAnonymous || context.HttpContext.GetCurrentAccount() != null)
        {
            base.OnActionExecuting(context);
            return;
        }

        var request = context.HttpContext.Request;
        var returnUrl = HttpMethods.IsGet(request.Method) ? $"{request.Path}{request.QueryString}" : null;

        context.Result = new RedirectToActionResult("Index", "Access", new { returnUrl });
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowAnonymousAccountAttribute : Attribute
{
}
