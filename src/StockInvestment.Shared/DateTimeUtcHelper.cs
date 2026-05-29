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
}
