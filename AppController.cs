using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public abstract class AppController : Controller
{
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    protected void Success(string message) => TempData["Success"] = message;

    protected void Fail(string message) => TempData["Error"] = message;

    /// <summary>Remembers who logged in (and their role) for all later pages.</summary>
    protected void StartSession(UserDtoLike user)
    {
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.Id);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);
        HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
        HttpContext.Session.SetString(SessionKeys.ProfilePicture, user.ProfilePictureUrl ?? string.Empty);
    }

    protected readonly record struct UserDtoLike(int Id, string Role, string FullName, string? ProfilePictureUrl);
}
