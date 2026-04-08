using System.Globalization;

namespace TgPcAgent.App.Services;

public static class MoscowTimeService
{
    private static readonly TimeZoneInfo MoscowTimeZone = ResolveTimeZone();

    public static string FormatTimestamp(DateTimeOffset value)
    {
        var localTime = TimeZoneInfo.ConvertTime(value, MoscowTimeZone);
        return localTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string FormatNow()
    {
        return $"{FormatTimestamp(DateTimeOffset.UtcNow)} МСК (UTC+3)";
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var timeZoneId in new[] { "Russian Standard Time", "Europe/Moscow" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
