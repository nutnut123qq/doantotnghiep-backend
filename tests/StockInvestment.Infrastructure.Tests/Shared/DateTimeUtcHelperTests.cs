using StockInvestment.Shared;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Shared;

public class DateTimeUtcHelperTests
{
    [Fact]
    public void ToUtcDate_NormalizesCalendarDateToUtcMidnight()
    {
        var input = new DateTime(2024, 5, 29, 15, 30, 0, DateTimeKind.Unspecified);

        var result = DateTimeUtcHelper.ToUtcDate(input);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2024, 5, 29, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void ToUtc_ConvertsUnspecifiedToUtc()
    {
        var input = new DateTime(2024, 5, 29, 12, 0, 0, DateTimeKind.Unspecified);

        var result = DateTimeUtcHelper.ToUtc(input);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }
}
