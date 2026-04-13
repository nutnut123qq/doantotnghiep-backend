using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Configuration;
using StockInvestment.Infrastructure.Services;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to crawl news periodically from Vietnamese financial news sources
/// P1-2: Uses distributed lock to prevent duplicate execution across instances
/// </summary>
public class NewsCrawlerJob : BackgroundService
{
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
        
        // P1-2: Acquire distributed lock
        var distributedLock = await JobLockHelper.TryAcquireLockAsync(
            scope, _configuration, _logger, "news-crawler", TimeSpan.FromHours(1), cancellationToken);
        
        if (distributedLock == null)
        {
            return; // Lock not acquired or disabled
        }

        try
        {
            var newsCrawlerService = scope.ServiceProvider.GetRequiredService<INewsCrawlerService>();
            var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
            _logger.LogInformation("Starting news crawl...");

            // Crawl news from all sources
            var maxPerRun = Math.Clamp(_ingestionOptions.MaxArticlesPerRun, 1, 500);
            var newsItems = await newsCrawlerService.CrawlNewsAsync(maxArticles: maxPerRun);
            var newsList = newsItems.ToList();

            if (!newsList.Any())
            {
                _logger.LogWarning("No news items crawled");
                return;
            }

            _logger.LogInformation("Crawled {Count} news items", newsList.Count);

            // Load existing URLs once into HashSet for efficient duplicate checking
            var existingUrls = await newsService.GetExistingUrlsAsync();
            var existingFingerprints = await newsService.GetExistingFingerprintsAsync();
            _logger.LogInformation("Loaded {Count} existing URLs for duplicate check", existingUrls.Count);

            // Check for duplicates before adding to database
            var addedCount = 0;
            var newsToAdd = new List<News>();

            foreach (var news in newsList)
            {
                try
                {
                    var canonicalUrl = CanonicalizeUrl(news.Url);
                    var fingerprint = NewsService.BuildFingerprint(news.Title, news.Source, news.PublishedAt);

                    // Skip if URL/fingerprint already exists
                    if ((!string.IsNullOrWhiteSpace(canonicalUrl) && existingUrls.Contains(canonicalUrl))
                        || (!string.IsNullOrWhiteSpace(fingerprint) && existingFingerprints.Contains(fingerprint)))
                    {
                        continue;
                    }

                    // Mark dedupe keys as seen to avoid duplicates within the same batch
                    if (!string.IsNullOrWhiteSpace(canonicalUrl))
                    {
                        news.Url = canonicalUrl;
                        existingUrls.Add(canonicalUrl);
                    }
                    if (!string.IsNullOrWhiteSpace(fingerprint))
                    {
                        existingFingerprints.Add(fingerprint);
                    }
                    newsToAdd.Add(news);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing news item: {Title}", news.Title);
                }
            }

            // Batch add all new news items
            if (newsToAdd.Any())
            {
                await newsService.AddNewsRangeAsync(newsToAdd);
                addedCount = newsToAdd.Count;
            }

            _logger.LogInformation("Successfully added {Count} new news items to database", addedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CrawlNewsAsync");
        }
        finally
        {
            // P1-2: Release distributed lock
            if (distributedLock != null)
            {
                await distributedLock.ReleaseAsync();
                distributedLock.Dispose();
            }
        }
    }

    private static string? CanonicalizeUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            return rawUrl.Trim();
        }

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

