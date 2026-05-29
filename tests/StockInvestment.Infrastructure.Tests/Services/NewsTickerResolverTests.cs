using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Services;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Services;

public class NewsTickerResolverTests
{
    [Fact]
    public void TryResolveTickerId_resolves_symbol_in_title()
    {
        var fptId = Guid.NewGuid();
        var tickerMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["FPT"] = fptId,
        };
        var aliasMap = NewsTickerResolver.BuildTickerNameAliasMap(Array.Empty<StockTicker>());

        var news = new News
        {
            Title = "FPT công b? k?t qu? kinh doanh quý 1",
            Content = "Chi ti?t báo cáo...",
            Source = "Test",
            PublishedAt = DateTime.UtcNow,
        };

        Assert.True(NewsTickerResolver.TryResolveTickerId(news, tickerMap, aliasMap, out var resolved));
        Assert.Equal(fptId, resolved);
    }

    [Fact]
    public void TryResolveTickerId_resolves_company_name_alias()
    {
        var fptId = Guid.NewGuid();
        var ticker = new StockTicker
        {
            Id = fptId,
            Symbol = "FPT",
            Name = "T?p ?oàn FPT",
            Exchange = Exchange.HOSE,
            CurrentPrice = 100m,
        };
        var tickerMap = NewsTickerResolver.BuildTickerMap(new[] { ticker });
        var aliasMap = NewsTickerResolver.BuildTickerNameAliasMap(new[] { ticker });

        var news = new News
        {
            Title = "T?p ?oàn FPT t?ng tr??ng m?nh trong quý",
            Content = "",
            Source = "Test",
            PublishedAt = DateTime.UtcNow,
        };

        Assert.True(NewsTickerResolver.TryResolveTickerId(news, tickerMap, aliasMap, out var resolved));
        Assert.Equal(fptId, resolved);
    }

    [Fact]
    public void GetSearchPhrasesForTicker_includes_symbol_and_name()
    {
        var ticker = new StockTicker
        {
            Symbol = "FPT",
            Name = "T?p ?oàn FPT",
            Exchange = Exchange.HOSE,
            CurrentPrice = 100m,
        };

        var phrases = NewsTickerResolver.GetSearchPhrasesForTicker(ticker);
        Assert.Contains("FPT", phrases);
        Assert.Contains("T?p ?oàn FPT", phrases);
    }
}
