using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Configuration;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.Services;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to crawl news periodically from Vietnamese financial news sources
/// P1-2: Uses distributed lock to prevent duplicate execution across instances
/// </summary>
public class NewsCrawlerJob : BackgroundService
{
    private static int _vn30RotationIndex;

    private readonly ILogger<NewsCrawlerJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly NewsIngestionOptions _ingestionOptions;

    public NewsCrawlerJob(
        ILogger<NewsCrawlerJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IOptions<NewsIngestionOptions> ingestionOptions)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _ingestionOptions = ingestionOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("News Crawler Job started");

        var initialDelay = Math.Clamp(_ingestionOptions.InitialDelaySeconds, 0, 3600);
        if (initialDelay > 0)
            await Task.Delay(TimeSpan.FromSeconds(initialDelay), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CrawlNewsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling news");
            }

            var pollMinutes = Math.Clamp(_ingestionOptions.PollMinutes, 1, 24 * 60);
            await Task.Delay(TimeSpan.FromMinutes(pollMinutes), stoppingToken);
        }

        _logger.LogInformation("News Crawler Job stopped");
    }

    private async Task CrawlNewsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var distributedLock = await JobLockHelper.TryAcquireLockAsync(
            scope, _configuration, _logger, "news-crawler", TimeSpan.FromHours(1), cancellationToken);

        if (distributedLock == null)
            return;

        try
        {
            var newsCrawlerService = scope.ServiceProvider.GetRequiredService<INewsCrawlerService>();
            var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            _logger.LogInformation("Starting news crawl...");

            var tickers = await dbContext.StockTickers.AsNoTracking().ToListAsync(cancellationToken);
            var tickerMap = NewsTickerResolver.BuildTickerMap(tickers);
            var aliasMap = NewsTickerResolver.BuildTickerNameAliasMap(tickers);
            var symbolToTicker = tickers
                .Where(t => !string.IsNullOrWhiteSpace(t.Symbol))
                .ToDictionary(t => t.Symbol.Trim().ToUpperInvariant(), t => t, StringComparer.OrdinalIgnoreCase);

            var newsList = new List<News>();

            var maxPerRun = Math.Clamp(_ingestionOptions.MaxArticlesPerRun, 1, 500);
            var generalItems = (await newsCrawlerService.CrawlNewsAsync(maxArticles: maxPerRun)).ToList();
            newsList.AddRange(generalItems);
            _logger.LogInformation("Crawled {Count} general news items", generalItems.Count);

            if (_ingestionOptions.SymbolCrawlEnabled)
            {
                var symbolItems = await CrawlVn30SymbolNewsAsync(
                    newsCrawlerService,
                    symbolToTicker,
                    cancellationToken);
                newsList.AddRange(symbolItems);
                _logger.LogInformation("Crawled {Count} symbol-specific news items", symbolItems.Count);
            }

            if (!newsList.Any())
            {
                _logger.LogWarning("No news items crawled (general + symbol)");
            }
            else
            {
                NewsTickerResolver.ApplyTickerTags(newsList, tickerMap, aliasMap);

                var existingUrls = await newsService.GetExistingUrlsAsync();
                var existingFingerprints = await newsService.GetExistingFingerprintsAsync();
                _logger.LogInformation("Loaded {Count} existing URLs for duplicate check", existingUrls.Count);

                var newsToAdd = new List<News>();
                foreach (var news in newsList)
                {
                    try
                    {
                        var canonicalUrl = CanonicalizeUrl(news.Url);
                        var fingerprint = NewsService.BuildFingerprint(news.Title, news.Source, news.PublishedAt);

                        if ((!string.IsNullOrWhiteSpace(canonicalUrl) && existingUrls.Contains(canonicalUrl))
                            || (!string.IsNullOrWhiteSpace(fingerprint) && existingFingerprints.Contains(fingerprint)))
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(canonicalUrl))
                        {
                            news.Url = canonicalUrl;
                            existingUrls.Add(canonicalUrl);
                        }

                        if (!string.IsNullOrWhiteSpace(fingerprint))
                            existingFingerprints.Add(fingerprint);

                        newsToAdd.Add(news);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing news item: {Title}", news.Title);
                    }
                }

                if (newsToAdd.Any())
                {
                    await newsService.AddNewsRangeAsync(newsToAdd);
                    _logger.LogInformation("Successfully added {Count} new news items to database", newsToAdd.Count);
                }
                else
                {
                    _logger.LogInformation("No new news items to add after dedupe");
                }
            }

            if (_ingestionOptions.BackfillTickerIdsEnabled)
            {
                var batchSize = Math.Clamp(_ingestionOptions.BackfillTickerIdsBatchSize, 1, 5000);
                var backfilled = await newsService.BackfillTickerIdsAsync(batchSize, cancellationToken);
                if (backfilled > 0)
                    _logger.LogInformation("Backfilled TickerId on {Count} existing news rows", backfilled);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CrawlNewsAsync");
        }
        finally
        {
            if (distributedLock != null)
            {
                await distributedLock.ReleaseAsync();
                distributedLock.Dispose();
            }
        }
    }

    private async Task<List<News>> CrawlVn30SymbolNewsAsync(
        INewsCrawlerService newsCrawlerService,
        IReadOnlyDictionary<string, StockTicker> symbolToTicker,
        CancellationToken cancellationToken)
    {
        var perRun = Math.Clamp(_ingestionOptions.Vn30SymbolsPerRun, 1, Vn30Universe.Symbols.Count);
        var maxPerSymbol = Math.Clamp(_ingestionOptions.MaxArticlesPerSymbol, 1, 20);
        var symbols = SelectVn30Symbols(perRun);
        var results = new List<News>();

        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var items = (await newsCrawlerService.CrawlNewsBySymbolAsync(symbol, maxPerSymbol)).ToList();
                if (symbolToTicker.TryGetValue(symbol, out var ticker))
                {
                    foreach (var news in items)
                        news.TickerId = ticker.Id;
                }

                results.AddRange(items);
                _logger.LogDebug("Symbol crawl {Symbol}: {Count} items", symbol, items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Symbol crawl failed for {Symbol}", symbol);
            }
        }

        return results;
    }

    internal static IReadOnlyList<string> SelectVn30Symbols(int count)
    {
        var all = Vn30Universe.Symbols;
        if (all.Count == 0)
            return Array.Empty<string>();

        count = Math.Clamp(count, 1, all.Count);
        var result = new List<string>(count);
        for (var i = 0; i < count; i++)
            result.Add(all[(_vn30RotationIndex + i) % all.Count]);

        _vn30RotationIndex = (_vn30RotationIndex + count) % all.Count;
        return result;
    }

    private static string? CanonicalizeUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            return rawUrl.Trim();

        var builder = new UriBuilder(uri)
        {
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };

        var blockedParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "fbclid", "gclid"
        };

        var keptParams = (builder.Query ?? string.Empty)
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part =>
            {
                var key = part.Split('=', 2)[0];
                return !blockedParams.Contains(key);
            })
            .ToArray();

        builder.Query = keptParams.Length == 0 ? string.Empty : string.Join("&", keptParams);
        var normalizedPath = builder.Path.TrimEnd('/');
        builder.Path = string.IsNullOrEmpty(normalizedPath) ? "/" : normalizedPath;

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}
