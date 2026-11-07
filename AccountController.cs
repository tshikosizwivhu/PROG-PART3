using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl, bool expired = false)
    {
        if (HttpContext.Session.IsLoggedIn()) return RedirectToHome();

        if (expired) Fail("Your session has ended. Please log in again.");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            HttpContext.Session.Clear(); // fresh session on every login
            var user = await _api.LoginAsync(model.Email, model.Password);
            StartSession(new UserDtoLike(user.Id, user.Role, user.FullName, user.ProfilePictureUrl));

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }
            return RedirectToHome();
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.IsLoggedIn()) return RedirectToHome();
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var isOrganiser = model.Role == Roles.Organiser;
        if (isOrganiser && string.IsNullOrWhiteSpace(model.OrganiserCode))
        {
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need the invite code.");
        }
        if (!ModelState.IsValid) return View(model);

        try
        {
            if (isOrganiser)
            {
                await _api.RegisterOrganiserAsync(model.FullName, model.Email, model.Password, model.OrganiserCode!);
            }
            else
            {
                await _api.RegisterParticipantAsync(model.FullName, model.Email, model.Password);
            }

            // Log the new user straight in.
            HttpContext.Session.Clear();
            var user = await _api.LoginAsync(model.Email, model.Password);
            StartSession(new UserDtoLike(user.Id, user.Role, user.FullName, user.ProfilePictureUrl));
            Success($"Welcome to RaceDay, {user.FullName}!");
            return RedirectToHome();
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _api.LogoutAsync();
        HttpContext.Session.Clear();
        Success("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToHome() =>
        CurrentRole == Roles.Organiser
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
}
