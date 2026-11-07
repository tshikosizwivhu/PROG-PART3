using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api)
    {
        _api = api;
    }

    /// <summary>Upcoming events with filtering by type and location. Open to everyone.</summary>
    public async Task<IActionResult> Index(string? eventType, string? location)
    {
        var model = new HomeViewModel
        {
            EventType = eventType,
            Location = location,
            Events = await _api.GetEventsAsync(eventType, location)
        };
        return View(model);
    }

    public IActionResult Problem(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "Something went wrong." : message;
        if (text.Length > 200) text = text[..200];
        return View(new ProblemViewModel { Message = text });
    }

    public IActionResult Error()
    {
        return View(new ProblemViewModel { Message = "Something unexpected went wrong. Please try again." });
    }
}
