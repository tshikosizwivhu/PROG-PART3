using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Everything an Organiser can do. All data comes from the Part 2 API; ownership is enforced there as well.</summary>
[RequireRole(Roles.Organiser)]
public class OrganiserController : AppController
{
    private static readonly Regex FinishTimePattern = new(@"^\d{1,2}:[0-5]\d:[0-5]\d$", RegexOptions.Compiled);

    private readonly ApiClient _api;

    public OrganiserController(ApiClient api)
    {
        _api = api;
    }

    // ---------------- Dashboard ----------------

    /// <summary>Summary of the organiser's events, total enrolments per event and upcoming dates.</summary>
    public async Task<IActionResult> Dashboard()
    {
        return View(new DashboardViewModel { Events = await _api.GetMyEventsAsync() });
    }

    // ---------------- Events ----------------

    public async Task<IActionResult> Events()
    {
        return View(await _api.GetMyEventsAsync());
    }

    [HttpGet]
    public IActionResult CreateEvent()
    {
        return View("EventForm", new EventFormViewModel { EventDate = DateTime.Today.AddDays(30) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("EventForm", model);

        try
        {
            var created = await _api.CreateEventAsync(ToRequest(model));
            var bannerProblem = await TryUploadBannerAsync(created.EventId, model.Banner);

            if (bannerProblem != null) Fail("The event was created, but the banner could not be uploaded: " + bannerProblem);
            else Success("Event created.");
            return RedirectToAction(nameof(Events));
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("EventForm", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (ev.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        return View("EventForm", new EventFormViewModel
        {
            EventId = ev.EventId,
            EventName = ev.EventName,
            EventDate = ev.EventDate,
            Location = ev.Location,
            Description = ev.Description,
            EventType = ev.EventType,
            DistanceKm = ev.DistanceKm,
            CurrentBannerUrl = ev.BannerImageUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(EventFormViewModel model)
    {
        if (model.EventId == null) return RedirectToAction(nameof(Events));
        if (!ModelState.IsValid) return View("EventForm", model);

        try
        {
            await _api.UpdateEventAsync(model.EventId.Value, ToRequest(model));
            var bannerProblem = await TryUploadBannerAsync(model.EventId.Value, model.Banner);

            if (bannerProblem != null) Fail("The event was saved, but the banner could not be uploaded: " + bannerProblem);
            else Success("Event updated.");
            return RedirectToAction(nameof(Events));
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("EventForm", model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        try
        {
            await _api.DeleteEventAsync(id);
            Success("Event deleted.");
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            Fail(ex.Message);
        }
        return RedirectToAction(nameof(Events));
    }

    // ---------------- Categories ----------------

    [HttpGet]
    public async Task<IActionResult> Categories(int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);
        if (ev.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        return View(new CategoriesPageViewModel { Event = ev, Form = new CategoryFormViewModel { EventId = eventId } });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(CategoryFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            var page = new CategoriesPageViewModel { Event = await _api.GetEventAsync(form.EventId), Form = form };
            return View("Categories", page);
        }

        try
        {
            await _api.CreateCategoryAsync(form.EventId, ToRequest(form));
            Success("Category added.");
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            Fail(ex.Message);
        }
        return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int eventId, int categoryId)
    {
        var ev = await _api.GetEventAsync(eventId);
        if (ev.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        var category = ev.Categories.FirstOrDefault(c => c.CategoryId == categoryId);
        if (category == null) return RedirectToAction(nameof(Categories), new { eventId });

        return View(new CategoryFormViewModel
        {
            CategoryId = category.CategoryId,
            EventId = eventId,
            CategoryName = category.CategoryName,
            DistanceKm = category.DistanceKm,
            EntryFee = category.EntryFee,
            MaxParticipants = category.MaxParticipants
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(CategoryFormViewModel form)
    {
        if (form.CategoryId == null) return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
        if (!ModelState.IsValid) return View(form);

        try
        {
            await _api.UpdateCategoryAsync(form.CategoryId.Value, ToRequest(form));
            Success("Category updated.");
            return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int eventId, int categoryId)
    {
        try
        {
            await _api.DeleteCategoryAsync(categoryId);
            Success("Category deleted.");
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            Fail(ex.Message);
        }
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // ---------------- Enrolments ----------------

    /// <summary>All participants enrolled for an event, with their category and enrolment status.</summary>
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);
        if (ev.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        return View(new EnrolmentsPageViewModel
        {
            Event = ev,
            Enrolments = await _api.GetEventEnrolmentsAsync(eventId)
        });
    }

    // ---------------- Results ----------------

    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);
        if (ev.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        var enrolments = await _api.GetEventEnrolmentsAsync(eventId);
        var results = await _api.GetEventResultsAsync(eventId);

        var rows = enrolments
            .Where(e => e.Status == "Confirmed")
            .Select(e => new ResultRowViewModel
            {
                Enrolment = e,
                Result = results.FirstOrDefault(r => r.EnrolmentId == e.EnrolmentId)
            })
            .ToList();

        return View(new ResultsPageViewModel { Event = ev, Rows = rows });
    }

    /// <summary>Captures (or updates) and publishes a participant's finish time and positions.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResult(ResultInputModel input)
    {
        TimeSpan? finishTime = null;
        if (!string.IsNullOrWhiteSpace(input.FinishTime))
        {
            var text = input.FinishTime.Trim();
            if (!FinishTimePattern.IsMatch(text) || !TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
            {
                Fail("Enter the finish time as hh:mm:ss, for example 00:24:15.");
                return RedirectToAction(nameof(Results), new { eventId = input.EventId });
            }
            finishTime = parsed;
        }

        if (finishTime == null && input.OverallPosition == null && input.CategoryPosition == null)
        {
            Fail("Enter a finish time or a position before saving.");
            return RedirectToAction(nameof(Results), new { eventId = input.EventId });
        }

        var request = new ResultUpsertRequest
        {
            FinishTime = finishTime,
            OverallPosition = input.OverallPosition,
            CategoryPosition = input.CategoryPosition
        };

        try
        {
            if (input.ResultId.HasValue) await _api.UpdateResultAsync(input.ResultId.Value, request);
            else await _api.CaptureResultAsync(input.EnrolmentId, request);
            Success("Result saved.");
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            Fail(ex.Message);
        }
        return RedirectToAction(nameof(Results), new { eventId = input.EventId });
    }

    // ---------------- Helpers ----------------

    private async Task<string?> TryUploadBannerAsync(int eventId, IFormFile? banner)
    {
        if (banner is not { Length: > 0 }) return null;

        try
        {
            await _api.UploadBannerAsync(eventId, banner);
            return null;
        }
        catch (ApiException ex) when (ex.StatusCode != StatusCodes.Status401Unauthorized)
        {
            return ex.Message;
        }
    }

    private static EventUpsertRequest ToRequest(EventFormViewModel model) => new()
    {
        EventName = model.EventName,
        EventDate = model.EventDate!.Value,
        Location = model.Location,
        Description = model.Description,
        EventType = model.EventType,
        DistanceKm = model.DistanceKm
    };

    private static CategoryUpsertRequest ToRequest(CategoryFormViewModel form) => new()
    {
        CategoryName = form.CategoryName,
        DistanceKm = form.DistanceKm,
        EntryFee = form.EntryFee,
        MaxParticipants = form.MaxParticipants
    };
}
