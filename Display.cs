using System.Globalization;

namespace RaceDay.Web.Services;

/// <summary>Small formatting helpers used by the views.</summary>
public static class Display
{
    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13) return number + "th";
        return (number % 10) switch
        {
            1 => number + "st",
            2 => number + "nd",
            3 => number + "rd",
            _ => number + "th"
        };
    }

    public static string FinishTime(TimeSpan? time) =>
        time.HasValue ? time.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "-";

    public static string Money(decimal amount) =>
        amount == 0 ? "Free" : "R" + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Date(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
