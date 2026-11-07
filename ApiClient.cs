using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// Talks to the Part 2 RaceDay API. All data operations in this MVC application go through this class.
/// The API identifies a user by its own session cookie; we keep that cookie in the user's MVC session
/// and send it with every request.
/// </summary>
public class ApiClient
{
    private const string ApiSessionCookieName = ".RaceDay.Session=";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _http;

    public ApiClient(HttpClient client, IHttpContextAccessor http)
    {
        _client = client;
        _http = http;
    }

    // ---------------- Authentication ----------------

    public async Task<UserDto> LoginAsync(string email, string password)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/auth/login",
            JsonContent.Create(new { email, password }, options: Json));
        await EnsureSuccessAsync(response);

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var sessionCookie = cookies
                .Select(c => c.Split(';')[0])
                .FirstOrDefault(c => c.StartsWith(ApiSessionCookieName, StringComparison.Ordinal));
            if (sessionCookie != null)
            {
                _http.HttpContext!.Session.SetString(SessionKeys.ApiCookie, sessionCookie);
            }
        }

        return (await response.Content.ReadFromJsonAsync<UserDto>(Json))!;
    }

    public Task RegisterParticipantAsync(string fullName, string email, string password) =>
        SendJsonAsync(HttpMethod.Post, "api/auth/register/participant", new { fullName, email, password });

    public Task RegisterOrganiserAsync(string fullName, string email, string password, string organiserCode) =>
        SendJsonAsync(HttpMethod.Post, "api/auth/register/organiser", new { fullName, email, password, organiserCode });

    public async Task LogoutAsync()
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Post, "api/auth/logout");
        }
        catch (ApiException)
        {
            // The local session is cleared either way.
        }
    }

    // ---------------- Events ----------------

    public Task<List<EventDto>> GetEventsAsync(string? eventType, string? location, bool includePast = false)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(eventType)) query.Add("eventType=" + Uri.EscapeDataString(eventType));
        if (!string.IsNullOrWhiteSpace(location)) query.Add("location=" + Uri.EscapeDataString(location));
        if (includePast) query.Add("includePast=true");
        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return GetAsync<List<EventDto>>(url);
    }

    public Task<EventDetailDto> GetEventAsync(int id) => GetAsync<EventDetailDto>($"api/events/{id}");

    public Task<List<EventSummaryDto>> GetMyEventsAsync() => GetAsync<List<EventSummaryDto>>("api/events/mine");

    public Task<EventDto> CreateEventAsync(EventUpsertRequest request) =>
        SendJsonAsync<EventDto>(HttpMethod.Post, "api/events", request);

    public Task UpdateEventAsync(int id, EventUpsertRequest request) =>
        SendJsonAsync(HttpMethod.Put, $"api/events/{id}", request);

    public Task DeleteEventAsync(int id) => DeleteAsync($"api/events/{id}");

    public Task<string> UploadBannerAsync(int eventId, IFormFile file) =>
        UploadAsync($"api/events/{eventId}/banner", file);

    // ---------------- Categories ----------------

    public Task CreateCategoryAsync(int eventId, CategoryUpsertRequest request) =>
        SendJsonAsync(HttpMethod.Post, $"api/events/{eventId}/categories", request);

    public Task UpdateCategoryAsync(int categoryId, CategoryUpsertRequest request) =>
        SendJsonAsync(HttpMethod.Put, $"api/categories/{categoryId}", request);

    public Task DeleteCategoryAsync(int categoryId) => DeleteAsync($"api/categories/{categoryId}");

    // ---------------- Enrolments ----------------

    public Task EnrolAsync(int categoryId) =>
        SendJsonAsync(HttpMethod.Post, $"api/categories/{categoryId}/enrolments", null);

    public Task<List<EnrolmentDto>> GetMyEnrolmentsAsync() => GetAsync<List<EnrolmentDto>>("api/enrolments/my");

    public Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId) =>
        GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments");

    public Task CancelEnrolmentAsync(int enrolmentId) => DeleteAsync($"api/enrolments/{enrolmentId}");

    // ---------------- Results ----------------

    public Task<List<ResultDto>> GetMyResultsAsync() => GetAsync<List<ResultDto>>("api/results/my");

    public Task<List<ResultDto>> GetEventResultsAsync(int eventId) =>
        GetAsync<List<ResultDto>>($"api/events/{eventId}/results");

    public Task CaptureResultAsync(int enrolmentId, ResultUpsertRequest request) =>
        SendJsonAsync(HttpMethod.Post, $"api/enrolments/{enrolmentId}/results", request);

    public Task UpdateResultAsync(int resultId, ResultUpsertRequest request) =>
        SendJsonAsync(HttpMethod.Put, $"api/results/{resultId}", request);

    // ---------------- Profile ----------------

    public Task<UserDto> GetParticipantAsync(int id) => GetAsync<UserDto>($"api/participants/{id}");

    public Task<UserDto> UpdateParticipantAsync(int id, string fullName, string email) =>
        SendJsonAsync<UserDto>(HttpMethod.Put, $"api/participants/{id}", new { fullName, email });

    public Task<string> UploadProfilePictureAsync(int id, IFormFile file) =>
        UploadAsync($"api/participants/{id}/picture", file);

    // ---------------- Plumbing ----------------

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await SendAsync(HttpMethod.Get, url);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string url, object? body)
    {
        using var response = await SendAsync(method, url, body == null ? null : JsonContent.Create(body, options: Json));
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task SendJsonAsync(HttpMethod method, string url, object? body)
    {
        using var response = await SendAsync(method, url, body == null ? null : JsonContent.Create(body, options: Json));
        await EnsureSuccessAsync(response);
    }

    private async Task DeleteAsync(string url)
    {
        using var response = await SendAsync(HttpMethod.Delete, url);
        await EnsureSuccessAsync(response);
    }

    /// <summary>Sends the image to the API, which stores it in Azure Blob Storage. This application never talks to Azure directly.</summary>
    private async Task<string> UploadAsync(string url, IFormFile file)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(fileContent, "file", file.FileName);

        using var response = await SendAsync(HttpMethod.Post, url, form);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ImageUploadResultDto>(Json);
        return result?.Url ?? string.Empty;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        var cookie = _http.HttpContext?.Session.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        try
        {
            return await _client.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new ApiException("The RaceDay service is not reachable right now. Please try again in a moment.", 503);
        }
        catch (TaskCanceledException)
        {
            throw new ApiException("The RaceDay service took too long to respond. Please try again.", 504);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new ApiException(await ReadErrorAsync(response), (int)response.StatusCode);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Our API errors look like { "message": "..." }
                    if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    {
                        return message.GetString()!;
                    }

                    // Validation errors look like { "errors": { "Field": ["..."] } }
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = new List<string>();
                        foreach (var field in errors.EnumerateObject())
                        {
                            if (field.Value.ValueKind != JsonValueKind.Array) continue;
                            foreach (var item in field.Value.EnumerateArray())
                            {
                                var text = item.GetString();
                                if (!string.IsNullOrWhiteSpace(text)) messages.Add(text);
                            }
                        }
                        if (messages.Count > 0) return string.Join(" ", messages);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic messages below.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You do not have permission to do that.",
            HttpStatusCode.NotFound => "That item could not be found.",
            HttpStatusCode.ServiceUnavailable => "That service is temporarily unavailable.",
            _ => "Something went wrong. Please try again."
        };
    }
}
