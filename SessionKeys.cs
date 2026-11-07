namespace RaceDay.Web.Services;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string FullName = "FullName";
    public const string ProfilePicture = "ProfilePicture";
    public const string ApiCookie = "ApiCookie";
}

public static class SessionStateExtensions
{
    public static bool IsLoggedIn(this ISession session) => session.GetInt32(SessionKeys.UserId).HasValue;
}
