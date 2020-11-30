using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GtrackWeb.Filters;

/// <summary>
/// Ensures a company + year has been selected (desktop: the splash screen)
/// before a screen that operates on company data is shown. Redirects to the
/// company selector otherwise.
/// </summary>
public sealed class RequireCompanyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<CurrentUser>();
        if (!user.CompanySelected)
        {
            context.Result = new RedirectToActionResult("SelectCompany", "Account", null);
        }
        base.OnActionExecuting(context);
    }
}
