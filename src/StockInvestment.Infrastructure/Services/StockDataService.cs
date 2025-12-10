using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Services;

public class StockDataService : IStockDataService
{
    private readonly ILogger<StockDataService> _logger;
    private readonly List<StockTicker> _mockTickers;

    public StockDataService(ILogger<StockDataService> logger)
    {
        _logger = logger;
        _mockTickers = GenerateMockTickers();
    }

    public Task<IEnumerable<StockTicker>> GetTickersAsync(string? exchange = null, string? index = null, string? industry = null, Guid? watchlistId = null)
    {
        var result = _mockTickers.AsEnumerable();

        if (!string.IsNullOrEmpty(exchange))
        {
            var exchangeEnum = Enum.Parse<Exchange>(exchange, true);
            result = result.Where(t => t.Exchange == exchangeEnum);
        }

        if (!string.IsNullOrEmpty(industry))
        {
            result = result.Where(t => t.Industry == industry);
        }

        return Task.FromResult(result);
    }

    public Task<StockTicker?> GetTickerBySymbolAsync(string symbol)
    {
        var ticker = _mockTickers.FirstOrDefault(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(ticker);
    }

    public Task<StockTicker?> GetTickerByIdAsync(Guid id)
    {
        var ticker = _mockTickers.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(ticker);
    }

    private List<StockTicker> GenerateMockTickers()
    {
        var random = new Random();
        var tickers = new List<StockTicker>();

        var symbols = new[] { "VIC", "VNM", "VCB", "VRE", "VHM", "VGC", "VSH", "VCI", "VHC", "VND" };
        var industries = new[] { "Banking", "Retail", "Technology", "Real Estate", "Manufacturing" };

        foreach (var symbol in symbols)
        {
            var basePrice = random.Next(10000, 200000);
            var change = (decimal)(random.NextDouble() * 2000 - 1000);
            var changePercent = (change / basePrice) * 100;

            tickers.Add(new StockTicker
            {
                Id = Guid.NewGuid(),
                Symbol = symbol,
                Name = $"{symbol} Corporation",
                Exchange = Exchange.HOSE,
                Industry = industries[random.Next(industries.Length)],
                CurrentPrice = basePrice,
                PreviousClose = basePrice - change,
                Change = change,
                ChangePercent = changePercent,
                Volume = random.Next(1000000, 10000000),
                Value = basePrice * random.Next(1000000, 10000000),
                LastUpdated = DateTime.UtcNow.AddMinutes(-random.Next(0, 60))
            });
        }

        return tickers;
    }
}

