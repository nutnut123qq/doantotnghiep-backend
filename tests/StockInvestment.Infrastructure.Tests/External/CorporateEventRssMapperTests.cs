using System.ServiceModel.Syndication;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.External;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.External;

public class CorporateEventRssMapperTests
{
    [Fact]
    public void TryMapItem_returns_null_when_no_ticker_in_map()
    {
        var item = new SyndicationItem
        {
            Title = new TextSyndicationContent("Thị trường chung không nhắc mã"),
            Summary = new TextSyndicationContent("Nội dung chung"),
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/a")));

        var ev = CorporateEventRssMapper.TryMapItem(item, new Dictionary<string, Guid>(), null, "Feed");
        Assert.Null(ev);
    }

    [Fact]
    public void TryMapItem_maps_FPT_ket_qua_to_earnings()
    {
        var fptId = Guid.NewGuid();
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["FPT"] = fptId };

        var item = new SyndicationItem
        {
            Title = new TextSyndicationContent("FPT công bố kết quả kinh doanh quý 4"),
            Summary = new TextSyndicationContent("Doanh thu và lợi nhuận tăng"),
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/fpt-q4")));
        item.PublishDate = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero);

        var ev = CorporateEventRssMapper.TryMapItem(item, map, null, "TestFeed");
        Assert.NotNull(ev);
        Assert.Equal(fptId, ev!.StockTickerId);
        Assert.Equal(CorporateEventType.Earnings, ev.EventType);
        Assert.Equal("https://example.com/fpt-q4", ev.SourceUrl);
        Assert.Contains("FPT", ev.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void TryMapItem_maps_dividend_keywords()
    {
        var vicId = Guid.NewGuid();
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["VIC"] = vicId };

        var item = new SyndicationItem
        {
            Title = new TextSyndicationContent("VIC thông báo chi trả cổ tức bằng tiền"),
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/vic-div")));
        item.PublishDate = DateTimeOffset.UtcNow;

        var ev = CorporateEventRssMapper.TryMapItem(item, map, null, "TestFeed");
        Assert.NotNull(ev);
        Assert.IsType<DividendEvent>(ev);
        Assert.Equal(CorporateEventType.Dividend, ev!.EventType);
    }
}
