using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

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
    private readonly TimeSpan _crawlInterval = TimeSpan.FromMinutes(30); // Crawl every 30 minutes

    public NewsCrawlerJob(
        ILogger<NewsCrawlerJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("News Crawler Job started");

        // Wait 10 seconds before first run
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

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

            await Task.Delay(_crawlInterval, stoppingToken);
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
            var newsItems = await newsCrawlerService.CrawlNewsAsync(maxArticles: 50);
            var newsList = newsItems.ToList();

            if (!newsList.Any())
            {
                _logger.LogWarning("No news items crawled");
                return;
            }

            _logger.LogInformation("Crawled {Count} news items", newsList.Count);

            // Load existing URLs once into HashSet for efficient duplicate checking
            var existingUrls = await newsService.GetExistingUrlsAsync();
            _logger.LogInformation("Loaded {Count} existing URLs for duplicate check", existingUrls.Count);

            // Check for duplicates before adding to database
            var addedCount = 0;
            var newsToAdd = new List<News>();

            foreach (var news in newsList)
            {
                try
                {
                    // Skip if URL is null or already exists
                    if (string.IsNullOrWhiteSpace(news.Url) || existingUrls.Contains(news.Url))
                    {
                        continue;
                    }

                    // Mark URL as seen to avoid duplicates within the same batch
                    existingUrls.Add(news.Url);
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
}

