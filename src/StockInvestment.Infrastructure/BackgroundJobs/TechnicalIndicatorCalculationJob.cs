using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to calculate technical indicators periodically
/// P1-2: Uses distributed lock to prevent duplicate execution across instances
/// </summary>
public class TechnicalIndicatorCalculationJob : BackgroundService
{
    private readonly ILogger<TechnicalIndicatorCalculationJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _calculationInterval;
    private readonly TimeSpan _initialDelay;

    public TechnicalIndicatorCalculationJob(
        ILogger<TechnicalIndicatorCalculationJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        var intervalMinutes = _configuration.GetValue("BackgroundJobs:TechnicalIndicatorIntervalMinutes", 60);
        _calculationInterval = TimeSpan.FromMinutes(Math.Clamp(intervalMinutes, 5, 720));
        var initialDelaySeconds = _configuration.GetValue("BackgroundJobs:TechnicalIndicatorInitialDelaySeconds", 300);
        _initialDelay = TimeSpan.FromSeconds(Math.Clamp(initialDelaySeconds, 0, 3600));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Technical Indicator Calculation Job started");

        if (_initialDelay > TimeSpan.Zero)
        {
            _logger.LogInformation("Technical indicator job waiting {Delay} before first run", _initialDelay);
            await Task.Delay(_initialDelay, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CalculateIndicatorsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating technical indicators");
            }

            await Task.Delay(_calculationInterval, stoppingToken);
        }

        _logger.LogInformation("Technical Indicator Calculation Job stopped");
    }

    private async Task CalculateIndicatorsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        // P1-2: Acquire distributed lock
        var distributedLock = await JobLockHelper.TryAcquireLockAsync(
            scope, _configuration, _logger, "technical-indicator-calculation", TimeSpan.FromHours(2), cancellationToken);
        
        if (distributedLock == null)
        {
            return; // Lock not acquired or disabled
        }

        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var indicatorService = scope.ServiceProvider.GetRequiredService<ITechnicalIndicatorService>();
            var symbols = await GetSymbolsToProcessAsync(dbContext, cancellationToken);
            _logger.LogInformation("Calculating indicators for {Count} symbols", symbols.Count);

            foreach (var symbol in symbols)
            {
                try
                {
                    await ProcessSymbolIndicatorsAsync(dbContext, indicatorService, symbol, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating indicators for {Symbol}", symbol);
                }

                // Delay nhỏ giữa các mã để tránh rate limit
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            _logger.LogInformation("Completed calculating indicators for {Count} symbols", symbols.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CalculateIndicatorsAsync");
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

    private async Task<List<string>> GetSymbolsToProcessAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var allowed = Vn30Universe.Symbols.ToList();
        return await dbContext.StockTickers
            .Select(t => t.Symbol)
            .Where(s => allowed.Contains(s))
            .Distinct()
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessSymbolIndicatorsAsync(
        ApplicationDbContext dbContext,
        ITechnicalIndicatorService indicatorService,
        string symbol,
        CancellationToken cancellationToken)
    {
        var indicators = await indicatorService.CalculateAllIndicatorsAsync(symbol);

        var ticker = await dbContext.StockTickers
            .FirstOrDefaultAsync(t => t.Symbol == symbol, cancellationToken);

        if (ticker == null)
        {
            _logger.LogWarning("Ticker not found for symbol {Symbol}", symbol);
            return;
        }

        await SaveIndicatorsAsync(dbContext, ticker.Id, indicators, cancellationToken);
        _logger.LogDebug("Calculated {Count} indicators for {Symbol}", indicators.Count, symbol);
    }

    private async Task SaveIndicatorsAsync(
        ApplicationDbContext dbContext,
        Guid tickerId,
        List<Domain.Entities.TechnicalIndicator> indicators,
        CancellationToken cancellationToken)
    {
        // Xóa indicators cũ
        var oldIndicators = await dbContext.TechnicalIndicators
            .Where(i => i.TickerId == tickerId)
            .ToListAsync(cancellationToken);

        dbContext.TechnicalIndicators.RemoveRange(oldIndicators);

        // Thêm indicators mới
        foreach (var indicator in indicators)
        {
            indicator.TickerId = tickerId;
            await dbContext.TechnicalIndicators.AddAsync(indicator, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

