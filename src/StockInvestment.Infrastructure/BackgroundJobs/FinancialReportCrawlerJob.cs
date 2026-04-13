using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Configuration;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically crawls financial reports for top tickers (by LastUpdated) and persists with dedupe.
/// </summary>
public class FinancialReportCrawlerJob : BackgroundService
{
    private readonly ILogger<FinancialReportCrawlerJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly FinancialIngestionOptions _options;

    public FinancialReportCrawlerJob(
        ILogger<FinancialReportCrawlerJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IOptions<FinancialIngestionOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Financial Report Crawler Job started");

        if (!_options.Enabled)
        {
            _logger.LogInformation("Financial Report Crawler Job is disabled via FinancialIngestion:Enabled=false");
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }

            return;
        }

        var initialDelay = Math.Clamp(_options.InitialDelaySeconds, 0, 3600);
        if (initialDelay > 0)
            await Task.Delay(TimeSpan.FromSeconds(initialDelay), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCrawlAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in financial report crawl run");
            }

            var pollMinutes = Math.Clamp(_options.PollMinutes, 1, 24 * 60);
            await Task.Delay(TimeSpan.FromMinutes(pollMinutes), stoppingToken);
        }

        _logger.LogInformation("Financial Report Crawler Job stopped");
    }

    private async Task RunCrawlAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var distributedLock = await JobLockHelper.TryAcquireLockAsync(
            scope,
            _configuration,
            _logger,
            "financial-report-crawler",
            TimeSpan.FromHours(2),
            cancellationToken);

        if (distributedLock == null)
        {
            return;
        }

        try
        {
            var reportService = scope.ServiceProvider.GetRequiredService<IFinancialReportService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var topN = Math.Clamp(_options.TopTickersPerRun, 1, 200);
            var maxReports = Math.Clamp(_options.MaxReportsPerSymbol, 1, 50);

            var extra = (_options.AdditionalSymbols ?? [])
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();

            var fromDb = await db.StockTickers
                .AsNoTracking()
                .OrderByDescending(t => t.LastUpdated)
                .ThenBy(t => t.Symbol)
                .Select(t => t.Symbol)
                .ToListAsync(cancellationToken);

            var symbols = new List<string>(topN + extra.Count);
            foreach (var s in extra)
            {
                if (symbols.Count >= topN)
                    break;
                if (!symbols.Contains(s))
                    symbols.Add(s);
            }

            foreach (var s in fromDb)
            {
                if (symbols.Count >= topN)
                    break;
                if (!symbols.Contains(s))
                    symbols.Add(s);
            }

            if (symbols.Count == 0)
            {
                _logger.LogWarning("Financial crawl skipped: no tickers in StockTickers");
                return;
            }

            _logger.LogInformation(
                "Starting financial crawl for {Count} tickers (maxReportsPerSymbol={MaxReports})",
                symbols.Count,
                maxReports);

            var totalInserted = 0;
            foreach (var symbol in symbols)
            {
                try
                {
                    var inserted = await reportService.CrawlAndPersistReportsForSymbolAsync(
                        symbol,
                        maxReports,
                        cancellationToken);

                    var n = inserted.Count;
                    totalInserted += n;
                    if (n > 0)
                    {
                        _logger.LogInformation("Financial crawl {Symbol}: inserted {Count} report(s)", symbol, n);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Financial crawl {Symbol}: no new rows (source empty, ticker missing, or all duplicates)",
                            symbol);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Financial crawl failed for symbol {Symbol}", symbol);
                }
            }

            _logger.LogInformation(
                "Financial crawl run completed: processedSymbols={Processed}, totalInserted={TotalInserted}",
                symbols.Count,
                totalInserted);
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
