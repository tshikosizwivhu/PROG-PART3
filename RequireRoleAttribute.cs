using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects a page. Not logged in: redirected to Login. Logged in with the wrong role: redirected to Access Denied.
/// Use [RequireRole] for any logged-in user, or [RequireRole(Roles.Organiser)] for one role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    public string? Role { get; }

    public RequireRoleAttribute(string? role = null)
    {
        Role = role;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;

        if (!session.IsLoggedIn())
        {
            var request = context.HttpContext.Request;
            var returnUrl = HttpMethods.IsGet(request.Method) ? request.Path + request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (Role != null && !string.Equals(session.GetString(SessionKeys.Role), Role, StringComparison.Ordinal))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
