using System.ServiceModel.Syndication;
using Microsoft.Extensions.Logging;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Fetches and maps RSS/Atom feeds to <see cref="News"/> entities.
/// </summary>
public sealed class RssNewsFetcher
{
    private readonly ILogger<RssNewsFetcher> _logger;
    private readonly HttpClient _httpClient;

    public RssNewsFetcher(ILogger<RssNewsFetcher> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("NewsCrawler");
    }

    public async Task<IReadOnlyList<News>> FetchAsync(
        string feedUrl,
        string sourceDisplayName,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var list = new List<News>();
        if (string.IsNullOrWhiteSpace(feedUrl))
            return list;

        var feed = await RssFeedLoader.LoadFeedAsync(_httpClient, feedUrl, _logger, cancellationToken);
        if (feed == null)
            return list;

        return MapFeedToNews(feed, sourceDisplayName, maxItems);
    }

    /// <summary>Maps a loaded syndication feed to news rows (testable without HTTP).</summary>
    internal static IReadOnlyList<News> MapFeedToNews(SyndicationFeed feed, string sourceDisplayName, int maxItems)
    {
        var list = new List<News>();
        foreach (var item in feed.Items.Take(maxItems))
        {
            var link = RssSyndicationUtilities.GetItemLink(item);
            if (string.IsNullOrWhiteSpace(link))
                continue;

            var title = item.Title?.Text?.Trim() ?? "";
            var summary = RssSyndicationUtilities.GetSummaryText(item);
            var published = RssSyndicationUtilities.ResolvePublishedAtUtc(item);

            list.Add(new News
            {
                Title = string.IsNullOrEmpty(title) ? link : title,
                Content = summary,
                Summary = string.IsNullOrWhiteSpace(summary) ? null : summary,
                Source = sourceDisplayName,
                Url = link,
                PublishedAt = published,
                CreatedAt = DateTime.UtcNow
            });
        }

        return list;
    }
}
