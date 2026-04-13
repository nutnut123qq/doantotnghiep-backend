using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Configuration;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Service to crawl news from Vietnamese financial news sources (HTML, RSS, configurable).
/// </summary>
public class NewsCrawlerService : INewsCrawlerService
{
    private readonly ILogger<NewsCrawlerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly NewsIngestionOptions _options;
    private readonly RssNewsFetcher _rssFetcher;

    public NewsCrawlerService(
        ILogger<NewsCrawlerService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<NewsIngestionOptions> options,
        RssNewsFetcher rssFetcher)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("NewsCrawler");
        _options = options.Value;
        _rssFetcher = rssFetcher;
    }

    public async Task<IEnumerable<News>> CrawlNewsAsync(int maxArticles = 20)
    {
        var cap = _options.MaxArticlesPerRun > 0
            ? Math.Min(maxArticles, _options.MaxArticlesPerRun)
            : maxArticles;

        try
        {
            if (_options.Sources is not { Count: > 0 })
                return FilterByBlockedUrlPaths(await CrawlLegacyParallelAsync(cap));

            var tasks = _options.Sources
                .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Name))
                .OrderBy(s => s.Priority)
                .Select(s => CrawlConfiguredSourceWithFallbackAsync(s, _options.MinItemsBeforeFallback));
            var results = await Task.WhenAll(tasks);
            return FilterByBlockedUrlPaths(results.SelectMany(x => x))
                .OrderByDescending(n => n.PublishedAt)
                .Take(cap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling news from configured sources");
            return Array.Empty<News>();
        }
    }

    private async Task<IReadOnlyList<News>> CrawlConfiguredSourceWithFallbackAsync(
        NewsSourceConfig source,
        int pipelineMinItemsBeforeFallback,
        HashSet<string>? visited = null)
    {
        var key = BuildSourceKey(source);
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!visited.Add(key))
        {
            _logger.LogWarning("Detected recursive news fallback config for source {SourceName}. Skipping repeated source.", source.Name);
            return Array.Empty<News>();
        }

        var fetched = (await CrawlConfiguredSourceAsync(source)).ToList();
        var minItemsBeforeFallback = Math.Max(0, source.MinItemsBeforeFallback ?? pipelineMinItemsBeforeFallback);
        _logger.LogInformation(
            "News source {SourceName} fetched={FetchedCount} threshold={Threshold} fallbackCandidates={FallbackCount}",
            source.Name,
            fetched.Count,
            minItemsBeforeFallback,
            source.FallbackSources.Count);

        if (fetched.Count >= minItemsBeforeFallback || source.FallbackSources.Count == 0)
        {
            return fetched;
        }

        var aggregate = new List<News>(fetched);
        var orderedFallbacks = source.FallbackSources
            .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Name))
            .OrderBy(s => s.Priority)
            .ToList();

        _logger.LogWarning(
            "News source {SourceName} returned too few items ({FetchedCount} < {Threshold}). Trying {FallbackCount} fallback sources.",
            source.Name,
            fetched.Count,
            minItemsBeforeFallback,
            orderedFallbacks.Count);

        foreach (var fallback in orderedFallbacks)
        {
            var nestedVisited = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
            var fallbackItems = await CrawlConfiguredSourceWithFallbackAsync(
                fallback,
                pipelineMinItemsBeforeFallback,
                nestedVisited);
            aggregate.AddRange(fallbackItems);

            if (aggregate.Count >= minItemsBeforeFallback)
            {
                break;
            }
        }

        return aggregate;
    }

    private async Task<IEnumerable<News>> CrawlConfiguredSourceAsync(NewsSourceConfig source)
    {
        var max = source.MaxItems ?? 20;
        var kind = source.Kind.Trim();

        try
        {
            if (kind.Equals("Rss", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(source.Url))
                {
                    _logger.LogWarning("RSS source {Name} has no Url; skipping.", source.Name);
                    return Array.Empty<News>();
                }

                return await _rssFetcher.FetchAsync(source.Url, source.Name, max);
            }

            if (kind.Equals("HtmlBuiltin", StringComparison.OrdinalIgnoreCase))
                return await CrawlHtmlBuiltinAsync(source.HtmlTemplate, max);

            if (kind.Equals("HtmlGeneric", StringComparison.OrdinalIgnoreCase))
                return await CrawlHtmlGenericAsync(source, max);

            _logger.LogWarning("Unknown news source Kind {Kind} for {Name}; skipping.", source.Kind, source.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Source {Name} failed", source.Name);
        }

        return Array.Empty<News>();
    }

    private static string BuildSourceKey(NewsSourceConfig source)
        => $"{source.Name}|{source.Kind}|{source.Url}|{source.HtmlTemplate}";

    private async Task<IEnumerable<News>> CrawlHtmlBuiltinAsync(string? template, int maxArticles)
    {
        return template?.Trim().ToLowerInvariant() switch
        {
            "cafef" => await CrawlCafeFAsync(maxArticles),
            "vnexpress" => await CrawlVNExpressAsync(maxArticles),
            "vietstock" => await CrawlVietStockAsync(maxArticles),
            _ => Enumerable.Empty<News>()
        };
    }

    private async Task<IEnumerable<News>> CrawlHtmlGenericAsync(NewsSourceConfig cfg, int maxArticles)
    {
        if (string.IsNullOrWhiteSpace(cfg.Url) || string.IsNullOrWhiteSpace(cfg.ItemXPath))
            return Enumerable.Empty<News>();

        var newsList = new List<News>();
        var html = await _httpClient.GetStringAsync(cfg.Url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var nodes = doc.DocumentNode.SelectNodes(cfg.ItemXPath);
        if (nodes == null)
        {
            _logger.LogWarning("HtmlGeneric source {SourceName} matched 0 nodes. Url={Url}, ItemXPath={ItemXPath}", cfg.Name, cfg.Url, cfg.ItemXPath);
            return newsList;
        }

        _logger.LogInformation("HtmlGeneric source {SourceName} matched {NodeCount} nodes from {Url}", cfg.Name, nodes.Count, cfg.Url);

        foreach (var node in nodes.Take(maxArticles))
        {
            var titleNode = !string.IsNullOrWhiteSpace(cfg.TitleXPath)
                ? node.SelectSingleNode(cfg.TitleXPath)
                : node.SelectSingleNode(".//a");
            if (titleNode == null)
                continue;

            var title = HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(title))
                continue;
            string articleUrl;
            if (!string.IsNullOrWhiteSpace(cfg.LinkXPath))
            {
                var linkNode = node.SelectSingleNode(cfg.LinkXPath);
                articleUrl = linkNode?.GetAttributeValue("href", "") ?? "";
            }
            else if (titleNode.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                articleUrl = titleNode.GetAttributeValue("href", "");
            }
            else
            {
                articleUrl = titleNode.SelectSingleNode(".//a")?.GetAttributeValue("href", "") ?? "";
            }

            if (string.IsNullOrWhiteSpace(articleUrl))
                continue;

            if (!articleUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(cfg.BaseUrl))
            {
                articleUrl = articleUrl.StartsWith('/')
                    ? cfg.BaseUrl.TrimEnd('/') + articleUrl
                    : cfg.BaseUrl.TrimEnd('/') + "/" + articleUrl.TrimStart('/');
            }

            var description = "";
            if (!string.IsNullOrWhiteSpace(cfg.SummaryXPath))
                description = HtmlEntity.DeEntitize(node.SelectSingleNode(cfg.SummaryXPath)?.InnerText ?? "").Trim();

            var timeText = "";
            if (!string.IsNullOrWhiteSpace(cfg.TimeXPath))
                timeText = node.SelectSingleNode(cfg.TimeXPath)?.InnerText.Trim() ?? "";

            var published = TryParseGenericTime(timeText);
            newsList.Add(CreateNews(title, description, cfg.Name, articleUrl, published));
        }

        _logger.LogInformation("HtmlGeneric source {SourceName} parsed {ParsedCount} news items", cfg.Name, newsList.Count);
        return newsList;
    }

    private static DateTime TryParseGenericTime(string timeText)
    {
        if (string.IsNullOrWhiteSpace(timeText))
            return DateTime.UtcNow;

        if (timeText.Contains("giờ trước", StringComparison.Ordinal))
        {
            var m = Regex.Match(timeText, @"\d+");
            if (m.Success && int.TryParse(m.Value, out var hours))
                return DateTime.UtcNow.AddHours(-hours);
        }

        if (timeText.Contains("phút trước", StringComparison.Ordinal))
        {
            var m = Regex.Match(timeText, @"\d+");
            if (m.Success && int.TryParse(m.Value, out var minutes))
                return DateTime.UtcNow.AddMinutes(-minutes);
        }

        if (DateTime.TryParse(timeText, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();

        return DateTime.UtcNow;
    }

    private async Task<IEnumerable<News>> CrawlLegacyParallelAsync(int maxArticles)
    {
        var per = Math.Max(5, maxArticles / 3);
        var tasks = new[]
        {
            CrawlCafeFAsync(per),
            CrawlVNExpressAsync(per),
            CrawlVietStockAsync(per)
        };
        var results = await Task.WhenAll(tasks);
        return FilterByBlockedUrlPaths(results.SelectMany(x => x))
            .OrderByDescending(n => n.PublishedAt)
            .Take(maxArticles);
    }

    private IEnumerable<News> FilterByBlockedUrlPaths(IEnumerable<News> items)
    {
        var blocked = _options.BlockedUrlPathSegments;
        foreach (var n in items)
        {
            if (NewsUrlPathFilter.IsAllowed(n.Url, blocked))
                yield return n;
        }
    }

    private static News CreateNews(string title, string description, string source, string url, DateTime publishedAt)
    {
        var d = description?.Trim() ?? "";
        return new News
        {
            Title = title,
            Content = d,
            Summary = string.IsNullOrWhiteSpace(d) ? null : d,
            Source = source,
            Url = url,
            PublishedAt = publishedAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<News>> CrawlNewsBySymbolAsync(string symbol, int maxArticles = 10)
    {
        var allNews = new List<News>();

        try
        {
            var tasks = new[]
            {
                CrawlCafeFBySymbolAsync(symbol, maxArticles / 2),
                CrawlVietStockBySymbolAsync(symbol, maxArticles / 2)
            };

            var results = await Task.WhenAll(tasks);

            foreach (var newsItems in results)
                allNews.AddRange(newsItems);

            return FilterByBlockedUrlPaths(allNews)
                .OrderByDescending(n => n.PublishedAt)
                .Take(maxArticles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling news for symbol {Symbol}", symbol);
            return FilterByBlockedUrlPaths(allNews)
                .OrderByDescending(n => n.PublishedAt)
                .Take(maxArticles);
        }
    }

    public async Task<IEnumerable<News>> CrawlFromSourceAsync(string source, int maxArticles = 20)
    {
        var batch = source.ToLower() switch
        {
            "cafef" => await CrawlCafeFAsync(maxArticles),
            "vnexpress" => await CrawlVNExpressAsync(maxArticles),
            "vietstock" => await CrawlVietStockAsync(maxArticles),
            _ => Enumerable.Empty<News>()
        };
        return FilterByBlockedUrlPaths(batch).Take(maxArticles);
    }

    private async Task<IEnumerable<News>> CrawlCafeFAsync(int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = "https://cafef.vn/thi-truong-chung-khoan.chn";
            _logger.LogDebug("Crawling CafeF from URL: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CafeF returned status {StatusCode} for URL: {Url}. Skipping this source.", response.StatusCode, url);
                return newsList;
            }

            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='tlitem']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='sapo']");
                    var timeNode = node.SelectSingleNode(".//span[@class='time']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";
                    var timeText = timeNode?.InnerText.Trim() ?? "";

                    if (!articleUrl.StartsWith("http"))
                        articleUrl = "https://cafef.vn" + articleUrl;

                    newsList.Add(CreateNews(title, description, "CafeF", articleUrl, ParseCafeFTime(timeText)));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CafeF news item");
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogWarning("HTTP error crawling CafeF: {Message}. This source may be temporarily unavailable.", httpEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling CafeF");
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlVNExpressAsync(int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = "https://vnexpress.net/kinh-doanh/chung-khoan";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsNodes = doc.DocumentNode.SelectNodes("//article[contains(@class,'item-news')]");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode =
                        node.SelectSingleNode(".//h3[contains(@class,'title-news')]/a")
                        ?? node.SelectSingleNode(".//h2[contains(@class,'title-news')]/a")
                        ?? node.SelectSingleNode(".//a[contains(@href,'.html')]");
                    var descNode = node.SelectSingleNode(".//p[@class='description']");
                    var timeNode = node.SelectSingleNode(".//span[@class='time']");

                    if (titleNode == null)
                        continue;

                    var title = HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(articleUrl))
                        continue;
                    var description = descNode?.InnerText.Trim() ?? "";
                    var timeText = timeNode?.InnerText.Trim() ?? "";

                    newsList.Add(CreateNews(title, description, "VNExpress", articleUrl, ParseVNExpressTime(timeText)));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing VNExpress news item");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling VNExpress");
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlVietStockAsync(int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = "https://vietstock.vn/chung-khoan.htm";
            _logger.LogDebug("Crawling VietStock from URL: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("VietStock returned status {StatusCode} for URL: {Url}. Skipping this source.", response.StatusCode, url);
                return newsList;
            }

            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='news-item']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='news-summary']");
                    var timeNode = node.SelectSingleNode(".//span[@class='news-time']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";
                    var timeText = timeNode?.InnerText.Trim() ?? "";

                    if (!articleUrl.StartsWith("http"))
                        articleUrl = "https://vietstock.vn" + articleUrl;

                    newsList.Add(CreateNews(title, description, "VietStock", articleUrl, ParseVietStockTime(timeText)));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing VietStock news item");
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogWarning("HTTP error crawling VietStock: {Message}. This source may be temporarily unavailable.", httpEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling VietStock");
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlCafeFBySymbolAsync(string symbol, int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = $"https://cafef.vn/tim-kiem.chn?keywords={symbol}";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='tlitem']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='sapo']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";

                    if (!articleUrl.StartsWith("http"))
                        articleUrl = "https://cafef.vn" + articleUrl;

                    newsList.Add(CreateNews(title, description, "CafeF", articleUrl, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CafeF search result");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CafeF for symbol {Symbol}", symbol);
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlVietStockBySymbolAsync(string symbol, int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = $"https://vietstock.vn/tim-kiem?q={symbol}";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='news-item']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='news-summary']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";

                    if (!articleUrl.StartsWith("http"))
                        articleUrl = "https://vietstock.vn" + articleUrl;

                    newsList.Add(CreateNews(title, description, "VietStock", articleUrl, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing VietStock search result");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching VietStock for symbol {Symbol}", symbol);
        }

        return newsList;
    }

    private DateTime ParseCafeFTime(string timeText)
    {
        try
        {
            if (timeText.Contains("giờ trước"))
            {
                var hours = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddHours(-hours);
            }

            if (timeText.Contains("phút trước"))
            {
                var minutes = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddMinutes(-minutes);
            }

            if (timeText.Contains('/'))
                return DateTime.Parse(timeText);
        }
        catch
        {
            // Ignore parsing errors
        }

        return DateTime.UtcNow;
    }

    private DateTime ParseVNExpressTime(string timeText)
    {
        try
        {
            if (timeText.Contains("giờ trước"))
            {
                var hours = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddHours(-hours);
            }

            if (timeText.Contains("phút trước"))
            {
                var minutes = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddMinutes(-minutes);
            }

            if (timeText.Contains('/'))
                return DateTime.Parse(timeText);
        }
        catch
        {
            // Ignore parsing errors
        }

        return DateTime.UtcNow;
    }

    private DateTime ParseVietStockTime(string timeText)
    {
        try
        {
            if (!string.IsNullOrEmpty(timeText))
                return DateTime.Parse(timeText);
        }
        catch
        {
            // Ignore parsing errors
        }

        return DateTime.UtcNow;
    }
}
