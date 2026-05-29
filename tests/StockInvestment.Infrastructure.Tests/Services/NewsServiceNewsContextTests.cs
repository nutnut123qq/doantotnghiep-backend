using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.Services;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Services;

public class NewsServiceNewsContextTests
{
    [Fact]
    public async Task GetRecentNewsForSymbolAsync_returns_tagged_news_by_TickerId()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"news_ctx_{Guid.NewGuid():N}")
            .Options;

        await using var ctx = new ApplicationDbContext(options);
        var ticker = new StockTicker
        {
            Symbol = "FPT",
            Name = "T?p ?oàn FPT",
            Exchange = Exchange.HOSE,
            CurrentPrice = 100m,
        };
        ctx.StockTickers.Add(ticker);
        ctx.News.Add(new News
        {
            TickerId = ticker.Id,
            Title = "Báo cáo tài chính",
            Content = "N?i dung",
            Source = "Test",
            PublishedAt = DateTime.UtcNow.AddDays(-1),
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var items = await service.GetRecentNewsForSymbolAsync("FPT", days: 14, limit: 5);

        Assert.Single(items);
        Assert.Equal("Báo cáo tài chính", items[0].Title);
    }

    [Fact]
    public async Task GetRecentNewsForSymbolAsync_matches_alias_without_ticker_id()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"news_ctx_{Guid.NewGuid():N}")
            .Options;

        await using var ctx = new ApplicationDbContext(options);
        var ticker = new StockTicker
        {
            Symbol = "FPT",
            Name = "T?p ?oàn FPT",
            Exchange = Exchange.HOSE,
            CurrentPrice = 100m,
        };
        ctx.StockTickers.Add(ticker);
        ctx.News.Add(new News
        {
            Title = "T?p ?oàn FPT công b? k?t qu? quý 1",
            Content = "Chi ti?t",
            Source = "Test",
            PublishedAt = DateTime.UtcNow.AddDays(-2),
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var items = await service.GetRecentNewsForSymbolAsync("FPT", days: 14, limit: 5);

        Assert.Single(items);
    }

    [Fact]
    public async Task BackfillTickerIdsAsync_assigns_ticker_id()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"news_backfill_{Guid.NewGuid():N}")
            .Options;

        await using var ctx = new ApplicationDbContext(options);
        var ticker = new StockTicker
        {
            Symbol = "VIC",
            Name = "T?p ?oàn Vingroup",
            Exchange = Exchange.HOSE,
            CurrentPrice = 100m,
        };
        ctx.StockTickers.Add(ticker);
        var news = new News
        {
            Title = "VIC niêm y?t thêm c? phi?u",
            Content = "",
            Source = "Test",
            PublishedAt = DateTime.UtcNow.AddDays(-1),
        };
        ctx.News.Add(news);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var updated = await service.BackfillTickerIdsAsync(100);

        Assert.Equal(1, updated);
        await ctx.Entry(news).ReloadAsync();
        Assert.Equal(ticker.Id, news.TickerId);
    }

    private static NewsService CreateService(ApplicationDbContext ctx)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var aiService = new Mock<IAIService>();
        return new NewsService(
            NullLogger<NewsService>.Instance,
            unitOfWork.Object,
            ctx,
            aiService.Object);
    }
}
