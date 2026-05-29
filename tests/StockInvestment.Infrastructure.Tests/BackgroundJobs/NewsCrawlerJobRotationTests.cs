using StockInvestment.Infrastructure.BackgroundJobs;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.BackgroundJobs;

public class NewsCrawlerJobRotationTests
{
    [Fact]
    public void SelectVn30Symbols_rotates_through_universe()
    {
        var first = NewsCrawlerJob.SelectVn30Symbols(6).ToList();
        var second = NewsCrawlerJob.SelectVn30Symbols(6).ToList();

        Assert.Equal(6, first.Count);
        Assert.Equal(6, second.Count);
        Assert.NotEqual(first[0], second[0]);
        Assert.Equal(first.Count, first.Distinct(StringComparer.Ordinal).Count());
    }
}
