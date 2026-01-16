using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to generate AI insights periodically
/// </summary>
public class AIInsightGenerationJob : BackgroundService
{
    private readonly ILogger<AIInsightGenerationJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _generationInterval = TimeSpan.FromMinutes(30); // Generate every 30 minutes

    public AIInsightGenerationJob(
        ILogger<AIInsightGenerationJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Insight Generation Job started");

        // Wait 10 minutes before first run to allow system to initialize
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateInsightsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI insights");
            }

            await Task.Delay(_generationInterval, stoppingToken);
        }

        _logger.LogInformation("AI Insight Generation Job stopped");
    }

    private async Task GenerateInsightsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var insightService = scope.ServiceProvider.GetRequiredService<IAIInsightService>();

        try
        {
            // Get top 50-100 most traded stocks (by volume)
            var tickerIds = await GetTopTradedTickersAsync(dbContext, cancellationToken);
            _logger.LogInformation("Generating insights for {Count} tickers", tickerIds.Count);

            var insights = await insightService.GenerateInsightsBatchAsync(tickerIds, cancellationToken);

            _logger.LogInformation("Generated {Count} insights", insights.Count());

            // Cleanup old dismissed insights
            await insightService.CleanupOldDismissedInsightsAsync(7, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateInsightsAsync");
        }
    }

    private async Task<List<Guid>> GetTopTradedTickersAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Get top 100 tickers by volume (most traded)
        return await dbContext.StockTickers
            .Where(t => t.Volume.HasValue && t.Volume > 0)
            .OrderByDescending(t => t.Volume)
            .Take(100)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }
}
