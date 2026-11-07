using RaceDay.Web.Filters;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC. The filter turns API failures into friendly pages (or a redirect to Login when the session has expired).
builder.Services.AddControllersWithViews(options => options.Filters.Add<ApiExceptionFilter>());
builder.Services.AddHttpContextAccessor();

// Server-side session: remembers who is logged in, their role, and the API's own session cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// The ONLY way this application reaches data: the Part 2 API (no database access in this project).
// Cookies are handled manually (UseCookies = false) so each user's API session stays inside their own MVC session.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false
});

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
