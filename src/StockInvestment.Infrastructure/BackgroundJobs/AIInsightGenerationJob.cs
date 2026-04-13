using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Configuration;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to generate AI insights periodically
/// </summary>
public class AIInsightGenerationJob : BackgroundService
{
    private readonly ILogger<AIInsightGenerationJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AIInsightGenerationOptions _options;
    private TimeSpan _generationInterval;

    public AIInsightGenerationJob(
        ILogger<AIInsightGenerationJob> logger,
        IServiceProvider serviceProvider,
        IOptions<AIInsightGenerationOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _generationInterval = TimeSpan.FromMinutes(Math.Max(15, _options.IntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Insight Generation Job started");

        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "AI insight generation is disabled (AIInsights:Generation:Enabled=false). Hosted service will idle.");
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }

            return;
        }

        // Delay first run to allow system to initialize
        await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.StartupDelayMinutes)), stoppingToken);

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
            // Coverage metric: number of symbols currently present in active global feed.
            var coverageCount = await dbContext.AIInsights
                .Where(i => i.DismissedAt == null)
                .Select(i => i.TickerId)
                .Distinct()
                .CountAsync(cancellationToken);

            var targetCoverage = Math.Max(5, _options.ScheduledTopSymbols);
            var isWarmup = _options.EnableWarmupProfile && coverageCount < targetCoverage;
            var effectiveMaxGenerate = isWarmup
                ? Math.Max(_options.MaxGeneratePerRun, _options.WarmupMaxGeneratePerRun)
                : _options.MaxGeneratePerRun;
            var effectiveTtl = TimeSpan.FromMinutes(Math.Max(30, isWarmup ? _options.WarmupMinInsightTtlMinutes : _options.MinInsightTtlMinutes));

            _generationInterval = TimeSpan.FromMinutes(Math.Max(15, isWarmup ? _options.WarmupIntervalMinutes : _options.IntervalMinutes));

            // 1) Schedule: top traded symbols with coverage-first ordering.
            var tickerIds = await GetCoveragePriorityTickerIdsAsync(dbContext, targetCoverage, cancellationToken);
            _logger.LogInformation(
                "Hybrid scheduler candidates: {Count}. coverage={Coverage}/{TargetCoverage}. warmup={Warmup}.",
                tickerIds.Count,
                coverageCount,
                targetCoverage,
                isWarmup);

            // 2) Trigger: queue symbols with strong change/news
            await EnqueueTriggeredSymbolsAsync(dbContext, insightService, cancellationToken);

            // 3) Hybrid generation with TTL + budget guard
            var generatedCount = await insightService.GenerateGlobalInsightsHybridAsync(
                tickerIds,
                effectiveMaxGenerate,
                effectiveTtl,
                cancellationToken);
            _logger.LogInformation(
                "Hybrid insight generation completed. generated={GeneratedCount}, effectiveMaxGenerate={EffectiveMaxGenerate}, effectiveTtlMinutes={EffectiveTtlMinutes}, nextIntervalMinutes={NextIntervalMinutes}",
                generatedCount,
                effectiveMaxGenerate,
                (int)effectiveTtl.TotalMinutes,
                (int)_generationInterval.TotalMinutes);

            // Cleanup old dismissed insights
            await insightService.CleanupOldDismissedInsightsAsync(7, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateInsightsAsync");
        }
    }

    private async Task<List<Guid>> GetCoveragePriorityTickerIdsAsync(
        ApplicationDbContext dbContext,
        int targetCoverage,
        CancellationToken cancellationToken)
    {
        // Top traded universe used as scheduler baseline.
        var topTickers = await dbContext.StockTickers
            .Where(t => t.Volume.HasValue && t.Volume > 0)
            .OrderByDescending(t => t.Volume)
            .Take(targetCoverage)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var activeInsightTickerIds = await dbContext.AIInsights
            .Where(i => i.DismissedAt == null)
            .Select(i => i.TickerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeSet = activeInsightTickerIds.ToHashSet();
        var missing = topTickers.Where(id => !activeSet.Contains(id)).ToList();
        var existing = topTickers.Where(id => activeSet.Contains(id)).ToList();

        // Coverage-first priority: missing symbols are always evaluated first.
        return missing.Concat(existing).ToList();
    }

    private async Task EnqueueTriggeredSymbolsAsync(
        ApplicationDbContext dbContext,
        IAIInsightService insightService,
        CancellationToken cancellationToken)
    {
        var changeThreshold = Math.Abs(_options.TriggerChangePercent);
        var moverIds = await dbContext.StockTickers
            .Where(t => t.ChangePercent.HasValue && Math.Abs(t.ChangePercent.Value) >= changeThreshold)
            .Select(t => t.Id)
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var id in moverIds)
        {
            await insightService.EnqueueTickerForRefreshAsync(id, "price-change-trigger", cancellationToken);
        }

        var newsCutoff = DateTime.UtcNow.AddMinutes(-Math.Max(10, _options.TriggerNewsLookbackMinutes));
        var recentNewsTickerIds = await dbContext.News
            .Where(n => !n.IsDeleted && n.PublishedAt >= newsCutoff && n.TickerId.HasValue)
            .Select(n => n.TickerId!.Value)
            .Distinct()
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var id in recentNewsTickerIds)
        {
            await insightService.EnqueueTickerForRefreshAsync(id, "news-trigger", cancellationToken);
        }

        _logger.LogInformation(
            "Trigger enqueue summary: movers={Movers}, recentNewsTickers={NewsTickers}, changeThreshold={Threshold}",
            moverIds.Count,
            recentNewsTickerIds.Count,
            changeThreshold);
    }
}
