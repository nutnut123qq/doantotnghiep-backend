using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Services;

public class StockDataService : IStockDataService
{
    private readonly ILogger<StockDataService> _logger;
    private readonly IVNStockService _vnStockService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICacheService _cacheService;

    public StockDataService(
        ILogger<StockDataService> logger,
        IVNStockService vnStockService,
        ApplicationDbContext dbContext,
        ICacheService cacheService)
    {
        _logger = logger;
        _vnStockService = vnStockService;
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task<IEnumerable<StockTicker>> GetTickersAsync(string? exchange = null, string? index = null, string? industry = null, Guid? watchlistId = null)
    {
        try
        {
            // Nếu có watchlistId, lấy từ database
            if (watchlistId.HasValue)
            {
                var watchlist = await _dbContext.Watchlists
                    .Include(w => w.Tickers)
                    .FirstOrDefaultAsync(w => w.Id == watchlistId.Value);

                if (watchlist != null)
                {
                    return watchlist.Tickers;
                }
            }

            // Lấy từ cache hoặc VNStock API
            var cacheKey = $"tickers_{exchange}_{industry}";
            var cachedTickers = await _cacheService.GetAsync<List<StockTicker>>(cacheKey);

            if (cachedTickers != null)
            {
                return cachedTickers;
            }

            // Lấy dữ liệu từ VNStock
            var tickers = await _vnStockService.GetAllSymbolsAsync(exchange);
            var tickersList = tickers.ToList();

            // Filter theo industry nếu có
            if (!string.IsNullOrEmpty(industry))
            {
                tickersList = tickersList.Where(t => t.Industry?.Contains(industry, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            // Cache 5 phút
            await _cacheService.SetAsync(cacheKey, tickersList, TimeSpan.FromMinutes(5));

            return tickersList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tickers");
            // Fallback to mock data
            return GenerateMockTickers();
        }
    }

    public async Task<StockTicker?> GetTickerBySymbolAsync(string symbol)
    {
        try
        {
            var cacheKey = $"ticker_{symbol}";
            var cachedTicker = await _cacheService.GetAsync<StockTicker>(cacheKey);

            if (cachedTicker != null)
            {
                return cachedTicker;
            }

            var ticker = await _vnStockService.GetQuoteAsync(symbol);

            if (ticker != null)
            {
                // Cache 1 phút
                await _cacheService.SetAsync(cacheKey, ticker, TimeSpan.FromMinutes(1));
            }

            return ticker;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticker {Symbol}", symbol);
            return null;
        }
    }

    public async Task<StockTicker?> GetTickerByIdAsync(Guid id)
    {
        try
        {
            var ticker = await _dbContext.StockTickers.FindAsync(id);
            return ticker;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticker by ID {Id}", id);
            return null;
        }
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

