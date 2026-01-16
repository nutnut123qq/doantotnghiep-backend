using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to calculate technical indicators periodically
/// </summary>
public class TechnicalIndicatorCalculationJob : BackgroundService
{
    private readonly ILogger<TechnicalIndicatorCalculationJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _calculationInterval = TimeSpan.FromHours(1); // Tính toán mỗi 1 giờ

    public TechnicalIndicatorCalculationJob(
        ILogger<TechnicalIndicatorCalculationJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Technical Indicator Calculation Job started");

        // Wait 5 minutes before first run
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

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
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var indicatorService = scope.ServiceProvider.GetRequiredService<ITechnicalIndicatorService>();

        try
        {
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
    }

    private async Task<List<string>> GetSymbolsToProcessAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.StockTickers
            .Select(t => t.Symbol)
            .Distinct()
            .Take(50) // Giới hạn 50 mã để tránh quá tải
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

