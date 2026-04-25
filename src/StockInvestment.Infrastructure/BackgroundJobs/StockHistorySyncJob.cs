using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to synchronise daily OHLCV historical data into the database.
/// Runs independently of <see cref="StockPriceUpdateJob"/> because history
/// only needs refreshing once per day/hour while quotes change every minute.
/// </summary>
public class StockHistorySyncJob : BackgroundService
{
    private readonly ILogger<StockHistorySyncJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _syncInterval;
    private readonly int _lookbackDays;

    public StockHistorySyncJob(
        ILogger<StockHistorySyncJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;

        var intervalMinutes = _configuration.GetValue("BackgroundJobs:StockHistorySyncIntervalMinutes", 60);
        _syncInterval = TimeSpan.FromMinutes(Math.Clamp(intervalMinutes, 15, 1440));

        _lookbackDays = _configuration.GetValue("BackgroundJobs:StockHistorySyncLookbackDays", 90);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Stock History Sync Job started (interval={Interval}, lookback={Lookback}d)",
            _syncInterval, _lookbackDays);

        // Wait a bit so the service has fully warmed up before the first run.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncHistoryAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StockHistorySyncJob");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("Stock History Sync Job stopped");
    }

    private async Task SyncHistoryAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var distributedLock = await JobLockHelper.TryAcquireLockAsync(
            scope, _configuration, _logger, "stock-history-sync", TimeSpan.FromMinutes(30), cancellationToken);

        if (distributedLock == null)
        {
            return;
        }

        try
        {
            var vnStockService = scope.ServiceProvider.GetRequiredService<IVNStockService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var vn30Symbols = Vn30Universe.Symbols;

            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-_lookbackDays);

            _logger.LogInformation(
                "Syncing historical prices for {Count} symbols from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
                vn30Symbols.Count, startDate, endDate);

            int totalRows = 0;
            foreach (var symbol in vn30Symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var normalized = symbol.ToUpperInvariant();

                    // Check what we already have in the DB for this symbol in the range
                    var existing = (await unitOfWork.Repository<StockPrice>()
                        .FindAsync(
                            sp => sp.Symbol == normalized && sp.Date >= startDate && sp.Date <= endDate))
                        .Select(sp => sp.Date.Date)
                        .ToHashSet();

                    var data = (await vnStockService.GetHistoricalDataAsync(normalized, startDate, endDate))
                        .OrderBy(d => d.Date)
                        .ToList();

                    var missing = data
                        .Where(d => !existing.Contains(d.Date.Date))
                        .Select(d => new StockPrice
                        {
                            Symbol = normalized,
                            Date = d.Date.Date,
                            Open = d.Open,
                            High = d.High,
                            Low = d.Low,
                            Close = d.Close,
                            Volume = d.Volume,
                            UpdatedAt = DateTime.UtcNow
                        })
                        .ToList();

                    if (missing.Count > 0)
                    {
                        await unitOfWork.Repository<StockPrice>().AddRangeAsync(missing);
                        totalRows += missing.Count;
                        _logger.LogDebug(
                            "Inserted {Count} new price rows for {Symbol}",
                            missing.Count, normalized);
                    }
                    else
                    {
                        _logger.LogDebug("No new price rows for {Symbol}", normalized);
                    }

                    // Small delay to avoid hammering the external API
                    await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync history for {Symbol}; continuing", symbol);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "StockHistorySyncJob completed. Inserted {TotalRows} new rows.",
                totalRows);
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
