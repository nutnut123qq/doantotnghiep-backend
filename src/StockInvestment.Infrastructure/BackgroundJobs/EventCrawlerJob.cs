using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to crawl corporate events periodically
/// P1-2: Uses distributed lock to prevent duplicate execution across instances
/// </summary>
public class EventCrawlerJob : BackgroundService
{
    private readonly ILogger<EventCrawlerJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _crawlInterval = TimeSpan.FromHours(12); // Crawl every 12 hours

    public EventCrawlerJob(
        ILogger<EventCrawlerJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
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
        
        // P1-2: Check if distributed lock is enabled
        var lockEnabled = _configuration.GetValue<bool>("BackgroundJobs:EnableDistributedLock", defaultValue: true);
        IDistributedLock? distributedLock = null;

        if (lockEnabled)
        {
            var lockFactory = scope.ServiceProvider.GetRequiredService<Func<IDistributedLock>>();
            distributedLock = lockFactory();
            
            var lockKey = "event-crawler-job";
            var lockExpiry = TimeSpan.FromHours(2); // Lock expires in 2 hours (job should finish faster)
            
            var acquired = await distributedLock.TryAcquireAsync(lockKey, lockExpiry, cancellationToken);
            if (!acquired)
            {
                _logger.LogInformation("Event crawler job skipped - already running on another instance");
                return;
            }
        }

        try
        {
            var eventCrawlerService = scope.ServiceProvider.GetRequiredService<IEventCrawlerService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

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

            // P1-1: Batch check for duplicates and batch insert for better performance
            var addedCount = 0;
            var skippedCount = 0;

            // Group events by (TickerId, EventType, EventDate) for batch duplicate check
            var eventsToAdd = new List<CorporateEvent>();
            
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
                        skippedCount++;
                        continue;
                    }

                    eventsToAdd.Add(corporateEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking event existence for ticker {TickerId}, type {EventType}, date {EventDate}",
                        corporateEvent.StockTickerId, corporateEvent.EventType, corporateEvent.EventDate);
                }
            }

            // P1-1: Batch insert new events
            if (eventsToAdd.Any())
            {
                try
                {
                    // Add all events in batch
                    foreach (var evt in eventsToAdd)
                    {
                        await unitOfWork.CorporateEvents.CreateAsync(evt);
                    }
                    
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    addedCount = eventsToAdd.Count;
                    
                    _logger.LogInformation(
                        "Batch inserted {Count} new events. Skipped {SkippedCount} duplicates.",
                        addedCount, skippedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error batch inserting events to database");
                }
            }
            else
            {
                _logger.LogInformation("No new events to add. All {TotalCount} events were duplicates.", eventsList.Count);
            }

            _logger.LogInformation("Event crawl completed: {AddedCount} new events added, {SkippedCount} duplicates skipped", 
                addedCount, skippedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event crawl process");
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
