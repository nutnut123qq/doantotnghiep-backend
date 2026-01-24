using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Infrastructure.Hubs;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to update stock prices periodically
/// </summary>
public class StockPriceUpdateJob : BackgroundService
{
    private readonly ILogger<StockPriceUpdateJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(1); // Cập nhật mỗi 1 phút

    public StockPriceUpdateJob(
        ILogger<StockPriceUpdateJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stock Price Update Job started");

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
        var vnStockService = scope.ServiceProvider.GetRequiredService<IVNStockService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<StockInvestment.Application.Interfaces.IUnitOfWork>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TradingHub>>();

        try
        {
            // Lấy danh sách VN30 để cập nhật
            var vn30Symbols = new[]
            {
                "VIC", "VNM", "VCB", "VRE", "VHM", "GAS", "MSN", "BID", "CTG", "HPG",
                "TCB", "MBB", "VPB", "PLX", "SAB", "VJC", "GVR", "FPT", "POW", "SSI",
                "MWG", "HDB", "ACB", "TPB", "STB", "PDR", "VIB", "BCM", "KDH", "NVL"
            };

            _logger.LogInformation("Updating prices for {Count} stocks", vn30Symbols.Length);

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
                if (existingStocksDict.TryGetValue(quote.Symbol, out var existingStock))
                {
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
    }
}

