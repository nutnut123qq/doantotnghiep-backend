using Microsoft.EntityFrameworkCore;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.Data.Repositories;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Data;

public class CorporateEventRepositoryTests
{
    [Fact]
    public async Task ExistsBySourceUrlAsync_matches_case_insensitive()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"events_repo_{Guid.NewGuid():N}")
            .Options;

        await using var ctx = new ApplicationDbContext(options);
        var ticker = new StockTicker
        {
            Symbol = "FPT",
            Name = "FPT Corp",
            Exchange = Exchange.HOSE,
            CurrentPrice = 100m,
        };
        ctx.StockTickers.Add(ticker);
        await ctx.SaveChangesAsync();

        var ev = new EarningsEvent
        {
            StockTickerId = ticker.Id,
            EventDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Title = "Test",
            SourceUrl = "https://example.com/announce/1",
        };
        ctx.CorporateEvents.Add(ev);
        await ctx.SaveChangesAsync();

        var repo = new CorporateEventRepository(ctx);
        Assert.True(await repo.ExistsBySourceUrlAsync("https://example.com/announce/1"));
        Assert.True(await repo.ExistsBySourceUrlAsync("HTTPS://EXAMPLE.COM/ANNOUNCE/1"));
        Assert.False(await repo.ExistsBySourceUrlAsync("https://example.com/other"));
        Assert.False(await repo.ExistsBySourceUrlAsync(""));
    }
}
