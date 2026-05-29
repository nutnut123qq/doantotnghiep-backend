namespace StockInvestment.Shared;

public static class DateTimeUtcHelper
{
    /// <summary>
    /// Normalizes a calendar date for PostgreSQL timestamptz columns (midnight UTC).
    /// </summary>
    public static DateTime ToUtcDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    /// <summary>
    /// Ensures a DateTime value is UTC before persisting to PostgreSQL timestamptz.
    /// </summary>
    public static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    /// <summary>
    /// Formats a UTC timestamp for user-facing notifications (default: Asia/Ho_Chi_Minh, UTC+7).
    /// </summary>
    public static string FormatInTimeZone(
        DateTime utc,
        string format = "yyyy-MM-dd HH:mm:ss",
        string timeZoneId = "Asia/Ho_Chi_Minh")
    {
        var utcTime = ToUtc(utc);
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZone).ToString(format);
        }
        catch (TimeZoneNotFoundException)
        {
            return utcTime.AddHours(7).ToString(format);
        }
        catch (InvalidTimeZoneException)
        {
            return utcTime.AddHours(7).ToString(format);
        }
    }
}
