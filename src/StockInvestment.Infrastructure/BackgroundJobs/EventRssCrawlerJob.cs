using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically ingests corporate events from configured RSS feeds (see EventIngestion).
/// </summary>
public class EventRssCrawlerJob : BackgroundService
{
    private readonly ILogger<EventRssCrawlerJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public EventRssCrawlerJob(
        ILogger<EventRssCrawlerJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event RSS Crawler Job started");

        var initialDelaySeconds = _configuration.GetValue("EventIngestion:InitialDelaySeconds", 45);
        await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, initialDelaySeconds)), stoppingToken);

        var pollMinutes = Math.Max(1, _configuration.GetValue("EventIngestion:PollMinutes", 15));
        var interval = TimeSpan.FromMinutes(pollMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIngestionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event RSS ingestion job");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Event RSS Crawler Job stopped");
    }

    private async Task RunIngestionAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var lockEnabled = _configuration.GetValue("BackgroundJobs:EnableDistributedLock", true);
        IDistributedLock? distributedLock = null;

        if (lockEnabled)
        {
            var lockFactory = scope.ServiceProvider.GetRequiredService<Func<IDistributedLock>>();
            distributedLock = lockFactory();

            var acquired = await distributedLock.TryAcquireAsync(
                "event-rss-crawler-job",
                TimeSpan.FromHours(2),
                cancellationToken);
            if (!acquired)
            {
                _logger.LogDebug("Event RSS crawler skipped — lock held elsewhere");
                return;
            }
        }

        try
        {
            var ingestion = scope.ServiceProvider.GetRequiredService<IEventRssIngestionService>();
            await ingestion.IngestFromRssAsync(cancellationToken);
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
}
