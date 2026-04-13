using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using StockInvestment.Infrastructure.External;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.External;

public class RssNewsFetcherTests
{
    [Fact]
    public void MapFeedToNews_maps_rss20_item()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0">
              <channel>
                <title>Test channel</title>
                <item>
                  <title>CK Việt Nam tăng điểm</title>
                  <link>https://example.com/news/1</link>
                  <description>&lt;p&gt;Sapo ngắn&lt;/p&gt;</description>
                  <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
                </item>
              </channel>
            </rss>
            """;

        using var reader = XmlReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        var feed = SyndicationFeed.Load(reader);
        Assert.NotNull(feed);

        var news = RssNewsFetcher.MapFeedToNews(feed, "TestRSS", maxItems: 5);
        Assert.Single(news);
        var n = news[0];
        Assert.Equal("CK Việt Nam tăng điểm", n.Title);
        Assert.Equal("https://example.com/news/1", n.Url);
        Assert.Equal("TestRSS", n.Source);
        Assert.Contains("Sapo", n.Content);
        Assert.Equal(n.Content, n.Summary);
        Assert.Equal(2024, n.PublishedAt.Year);
    }
}
