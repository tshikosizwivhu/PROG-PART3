using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RequireRole(Roles.Participant)]
public class ParticipantController : AppController
{
    private readonly ApiClient _api;

    public ParticipantController(ApiClient api)
    {
        _api = api;
    }

    /// <summary>Every event the participant has enrolled for, with its status.</summary>
    public async Task<IActionResult> MyEvents()
    {
        return View(await _api.GetMyEnrolmentsAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _api.CancelEnrolmentAsync(id);
            Success("Your enrolment has been cancelled.");
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            Fail(ex.Message);
        }
        return RedirectToAction(nameof(MyEvents));
    }

    /// <summary>Race history: event, date, category, finish time and position (e.g. 47th out of 312 finishers).</summary>
    public async Task<IActionResult> MyResults()
    {
        return View(await _api.GetMyResultsAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _api.GetParticipantAsync(CurrentUserId);
        return View(new ProfileViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            ProfilePictureUrl = user.ProfilePictureUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        model.Id = CurrentUserId;
        if (!ModelState.IsValid) return View(model);

        try
        {
            var user = await _api.UpdateParticipantAsync(model.Id, model.FullName, model.Email);
            var pictureUrl = user.ProfilePictureUrl;

            if (model.Picture is { Length: > 0 })
            {
                try
                {
                    pictureUrl = await _api.UploadProfilePictureAsync(model.Id, model.Picture);
                }
                catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
                {
                    Fail("Your details were saved, but the picture could not be uploaded: " + ex.Message);
                    HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
                    return RedirectToAction(nameof(Profile));
                }
            }

            HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
            HttpContext.Session.SetString(SessionKeys.ProfilePicture, pictureUrl ?? string.Empty);
            Success("Your profile has been updated.");
            return RedirectToAction(nameof(Profile));
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
