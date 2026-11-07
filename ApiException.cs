namespace RaceDay.Web.Services;

/// <summary>An error returned by (or while calling) the RaceDay API, with a message that is safe to show to users.</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
