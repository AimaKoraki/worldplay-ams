namespace WorldplayAMS.UI.Helpers;

/// <summary>
/// Extension methods for converting UTC DateTimes to Sri Lanka Standard Time (UTC+5:30).
/// </summary>
public static class TimeHelper
{
    private static readonly TimeZoneInfo SriLankaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");

    /// <summary>
    /// Converts a UTC DateTime to Sri Lanka Standard Time (UTC+5:30).
    /// </summary>
    public static DateTime ToSriLankaTime(this DateTime utcDateTime)
    {
        var kind = utcDateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
            : utcDateTime;
        return TimeZoneInfo.ConvertTimeFromUtc(
            kind.Kind == DateTimeKind.Utc ? kind : kind.ToUniversalTime(),
            SriLankaTimeZone);
    }
}
