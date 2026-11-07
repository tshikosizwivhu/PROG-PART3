using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class EventsController : AppController
{
    private readonly ApiClient _api;

    public EventsController(ApiClient api)
    {
        _api = api;
    }

    /// <summary>Full event information and categories. Open to everyone; participants see enrol buttons.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var model = new EventDetailsViewModel { Event = await _api.GetEventAsync(id) };

        if (CurrentRole == Roles.Participant)
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            model.EnrolledCategoryIds = mine
                .Where(e => e.Status == "Confirmed")
                .Select(e => e.CategoryId)
                .ToHashSet();
        }

        return View(model);
    }

    /// <summary>Records the enrolment immediately through the API.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [RequireRole(Roles.Participant)]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        try
        {
            await _api.EnrolAsync(categoryId);
            Success("You are enrolled! Your enrolment is confirmed.");
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            Fail(ex.Message);
        }
        return RedirectToAction(nameof(Details), new { id = eventId });
    }
}
