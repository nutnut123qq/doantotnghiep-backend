using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Domain.Constants;
using StockInvestment.Infrastructure.Hubs;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to update stock prices periodically
/// P1-2: Uses distributed lock to prevent duplicate execution across instances
/// </summary>
public class StockPriceUpdateJob : BackgroundService
{
    private readonly ILogger<StockPriceUpdateJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(1); // Cập nhật mỗi 1 phút

    public StockPriceUpdateJob(
        ILogger<StockPriceUpdateJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stock Price Update Job started");

        var initialDelaySeconds = _configuration.GetValue("BackgroundJobs:StockPriceUpdateInitialDelaySeconds", 0);
        if (initialDelaySeconds < 0) initialDelaySeconds = 0;
        if (initialDelaySeconds > 600) initialDelaySeconds = 600;
        if (initialDelaySeconds > 0)
        {
            _logger.LogInformation(
                "Stock price update: waiting {Seconds}s before first run (BackgroundJobs:StockPriceUpdateInitialDelaySeconds)",
                initialDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateStockPricesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock prices");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }

        _logger.LogInformation("Stock Price Update Job stopped");
    }

    private async Task UpdateStockPricesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        // P1-2: Acquire distributed lock
        var distributedLock = await JobLockHelper.TryAcquireLockAsync(
            scope, _configuration, _logger, "stock-price-update", TimeSpan.FromMinutes(5), cancellationToken);
        
        if (distributedLock == null)
        {
            return; // Lock not acquired or disabled
        }

        try
        {
            var vnStockService = scope.ServiceProvider.GetRequiredService<IVNStockService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<StockInvestment.Application.Interfaces.IUnitOfWork>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TradingHub>>();
            var vn30Symbols = Vn30Universe.Symbols;

            _logger.LogInformation("Updating prices for {Count} stocks", vn30Symbols.Count);

            var quotes = await vnStockService.GetQuotesAsync(vn30Symbols);
            var quotesList = quotes.ToList();

            // Batch load all existing tickers by Symbol to avoid N+1 queries
            var symbols = quotesList.Select(q => q.Symbol).ToList();
            var existingStocks = (await unitOfWork.Repository<Domain.Entities.StockTicker>()
                .FindAsync(s => symbols.Contains(s.Symbol), cancellationToken))
                .ToList();

            var existingStocksDict = existingStocks.ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);

            // Update existing or add new tickers in-memory
            foreach (var quote in quotesList)
            {
                if (quote.CurrentPrice <= 0)
                {
                    _logger.LogWarning("Skipping quote update for {Symbol}: invalid current price {Price}", quote.Symbol, quote.CurrentPrice);
                    continue;
                }

                if (existingStocksDict.TryGetValue(quote.Symbol, out var existingStock))
                {
                    if (IsSuspiciousPriceScale(existingStock.CurrentPrice, quote.CurrentPrice))
                    {
                        _logger.LogWarning(
                            "Skipping quote update for {Symbol}: suspicious scale change old={OldPrice} new={NewPrice}",
                            quote.Symbol,
                            existingStock.CurrentPrice,
                            quote.CurrentPrice);
                        continue;
                    }

                    // Cập nhật giá
                    existingStock.CurrentPrice = quote.CurrentPrice;
                    existingStock.PreviousClose = quote.PreviousClose;
                    existingStock.Change = quote.Change;
                    existingStock.ChangePercent = quote.ChangePercent;
                    existingStock.Volume = quote.Volume;
                    existingStock.Value = quote.Value;
                    existingStock.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    // Thêm mới
                    await unitOfWork.Repository<Domain.Entities.StockTicker>().AddAsync(quote, cancellationToken);
                }
            }

            // Single SaveChanges for all updates
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Push update qua SignalR - gửi từng ticker với event "PriceUpdated"
            // Chỉ gửi tới group của symbol (clients cần join group để nhận updates)
            foreach (var quote in quotesList)
            {
                await hubContext.Clients.Group(quote.Symbol).SendAsync("PriceUpdated", quote, cancellationToken);
            }

            _logger.LogInformation("Successfully updated {Count} stock prices", quotesList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateStockPricesAsync");
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

    private static bool IsSuspiciousPriceScale(decimal previousPrice, decimal newPrice)
    {
        if (previousPrice <= 0 || newPrice <= 0)
        {
            return false;
        }

        var ratio = newPrice > previousPrice ? newPrice / previousPrice : previousPrice / newPrice;

        // DB may still hold thousand-VND style rows vs quotes in full VND (~1000x); allow update.
        if (ratio is >= 850m and <= 1150m)
        {
            return false;
        }

        return ratio >= 20m;
    }
}

