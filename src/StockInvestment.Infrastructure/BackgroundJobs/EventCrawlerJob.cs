using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to crawl corporate events periodically
/// </summary>
public class EventCrawlerJob : BackgroundService
{
    private readonly ILogger<EventCrawlerJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _crawlInterval = TimeSpan.FromHours(12); // Crawl every 12 hours

    public EventCrawlerJob(
        ILogger<EventCrawlerJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Crawler Job started");

        // Wait 20 seconds before first run
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CrawlEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling corporate events");
            }

            await Task.Delay(_crawlInterval, stoppingToken);
        }

        _logger.LogInformation("Event Crawler Job stopped");
    }

    private async Task CrawlEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var eventCrawlerService = scope.ServiceProvider.GetRequiredService<IEventCrawlerService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        try
        {
            _logger.LogInformation("Starting event crawl for next 60 days...");

            // Crawl upcoming events (next 60 days)
            var events = await eventCrawlerService.CrawlUpcomingEventsAsync(daysAhead: 60);
            var eventsList = events.ToList();

            if (!eventsList.Any())
            {
                _logger.LogWarning("No events crawled");
                return;
            }

            _logger.LogInformation("Crawled {Count} corporate events", eventsList.Count);

            // Check for duplicates before adding to database
            var addedCount = 0;
            var updatedCount = 0;

            foreach (var corporateEvent in eventsList)
            {
                try
                {
                    // Check if event already exists
                    var exists = await unitOfWork.CorporateEvents.ExistsAsync(
                        corporateEvent.StockTickerId,
                        corporateEvent.EventType,
                        corporateEvent.EventDate);

                    if (exists)
                    {
                        // Optional: Update existing event if details changed
                        // For now, skip duplicates
                        continue;
                    }

                    // Add new event to database
                    await unitOfWork.CorporateEvents.CreateAsync(corporateEvent);
                    addedCount++;

                    _logger.LogDebug("Added event: {EventType} for ticker {TickerId} on {EventDate}",
                        corporateEvent.EventType,
                        corporateEvent.StockTickerId,
                        corporateEvent.EventDate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error adding event to database");
                }
            }

            _logger.LogInformation("Event crawl completed: {AddedCount} new events added", addedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event crawl process");
        }
    }
}
