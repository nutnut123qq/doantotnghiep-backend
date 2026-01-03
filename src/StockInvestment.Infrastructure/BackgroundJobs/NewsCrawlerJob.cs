using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to crawl news periodically from Vietnamese financial news sources
/// </summary>
public class NewsCrawlerJob : BackgroundService
{
    private readonly ILogger<NewsCrawlerJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _crawlInterval = TimeSpan.FromMinutes(30); // Crawl every 30 minutes

    public NewsCrawlerJob(
        ILogger<NewsCrawlerJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
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
        var newsCrawlerService = scope.ServiceProvider.GetRequiredService<INewsCrawlerService>();
        var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();

        try
        {
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

            // Check for duplicates before adding to database
            var addedCount = 0;
            foreach (var news in newsList)
            {
                try
                {
                    // Simple duplicate check by URL
                    var existingNews = await newsService.GetNewsAsync(1, 1000);
                    var isDuplicate = existingNews.Any(n => n.Url == news.Url);

                    if (!isDuplicate)
                    {
                        await newsService.AddNewsAsync(news);
                        addedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error adding news item: {Title}", news.Title);
                }
            }

            _logger.LogInformation("Successfully added {Count} new news items to database", addedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CrawlNewsAsync");
        }
    }
}

