using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Catches API errors that a controller did not handle itself, so users never see raw error messages.
/// 401 = the API session ended: clear our session and send the user to Login.
/// Anything else: a friendly problem page.
/// </summary>
public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException exception) return;

        if (exception.StatusCode == StatusCodes.Status401Unauthorized)
        {
            context.HttpContext.Session.Clear();
            context.Result = new RedirectToActionResult("Login", "Account", new { expired = true });
        }
        else
        {
            context.Result = new RedirectToActionResult("Problem", "Home", new { message = exception.Message });
        }

        context.ExceptionHandled = true;
    }
}
