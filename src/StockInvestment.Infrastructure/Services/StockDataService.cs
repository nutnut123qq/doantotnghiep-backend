using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Services;

public class StockDataService : IStockDataService
{
    private readonly ILogger<StockDataService> _logger;
    private readonly IVNStockService _vnStockService;
    private readonly IStockTickerRepository _stockTickerRepository;
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly ICacheService _cacheService;

    public StockDataService(
        ILogger<StockDataService> logger,
        IVNStockService vnStockService,
        IStockTickerRepository stockTickerRepository,
        IWatchlistRepository watchlistRepository,
        ICacheService cacheService)
    {
        _logger = logger;
        _vnStockService = vnStockService;
        _stockTickerRepository = stockTickerRepository;
        _watchlistRepository = watchlistRepository;
        _cacheService = cacheService;
    }

    public async Task<IEnumerable<StockTicker>> GetTickersAsync(string? exchange = null, string? index = null, string? industry = null, Guid? watchlistId = null)
    {
        try
        {
            // Nếu có watchlistId, lấy từ database
            if (watchlistId.HasValue)
            {
                var watchlist = await _watchlistRepository.GetByIdWithTickersAsync(watchlistId.Value);

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

            // Ưu tiên lấy từ database trước (nếu có dữ liệu đã được cập nhật)
            var dbTickers = await _stockTickerRepository.GetTickersAsync(exchange, null, industry);
            var dbTickersList = dbTickers.ToList();

            // Nếu có dữ liệu trong DB và đã được cập nhật gần đây (trong vòng 10 phút), dùng dữ liệu từ DB
            if (dbTickersList.Any() && dbTickersList.Any(t => t.LastUpdated > DateTime.UtcNow.AddMinutes(-10)))
            {
                var result = dbTickersList.Where(t => t.LastUpdated > DateTime.UtcNow.AddMinutes(-10)).ToList();
                
                // Cache 2 phút
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2));
                
                return result;
            }

            // Nếu không có trong DB hoặc dữ liệu cũ, lấy từ VNStock API
            // Lấy danh sách symbols
            var symbols = await _vnStockService.GetAllSymbolsAsync(exchange);
            var symbolsList = symbols.ToList();

            // Filter theo industry nếu có
            if (!string.IsNullOrEmpty(industry))
            {
                symbolsList = symbolsList.Where(t => t.Industry?.Contains(industry, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            // Giới hạn số lượng để tránh quá chậm (lấy tối đa 100 symbols đầu tiên)
            var limitedSymbols = symbolsList.Take(100).Select(s => s.Symbol).ToList();

            // Lấy giá (quotes) cho các symbols này
            var quotes = await _vnStockService.GetQuotesAsync(limitedSymbols);
            var quotesList = quotes.ToList();

            // Merge với thông tin từ symbols (để có đầy đủ thông tin)
            var tickersList = quotesList.Select(quote =>
            {
                var symbolInfo = symbolsList.FirstOrDefault(s => s.Symbol == quote.Symbol);
                if (symbolInfo != null)
                {
                    // Cập nhật thông tin từ symbol nếu quote thiếu
                    quote.Name = symbolInfo.Name ?? quote.Name;
                    quote.Exchange = symbolInfo.Exchange;
                    quote.Industry = symbolInfo.Industry ?? quote.Industry;
                }
                return quote;
            }).ToList();

            // Nếu không lấy được quotes, fallback về symbols (nhưng sẽ có giá = 0)
            if (!tickersList.Any() && symbolsList.Any())
            {
                tickersList = symbolsList.Take(100).ToList();
            }

            // Cache 2 phút (ngắn hơn để cập nhật giá thường xuyên hơn)
            await _cacheService.SetAsync(cacheKey, tickersList, TimeSpan.FromMinutes(2));

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
            var ticker = await _stockTickerRepository.GetByIdAsync(id);
            return ticker;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticker by ID {Id}", id);
            return null;
        }
    }

    public async Task<Dictionary<string, StockTicker>> GetTickersBySymbolsAsync(IEnumerable<string> symbols)
    {
        try
        {
            var symbolsList = symbols.Distinct().ToList();
            if (!symbolsList.Any())
            {
                return new Dictionary<string, StockTicker>();
            }

            // Try to get from database first (batch query)
            var dbTickers = await _stockTickerRepository.GetBySymbolsAsync(symbolsList);

            var result = new Dictionary<string, StockTicker>();
            var missingSymbols = new List<string>();

            // Add tickers found in database
            foreach (var kvp in dbTickers)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Find missing symbols
            foreach (var symbol in symbolsList)
            {
                if (!result.ContainsKey(symbol))
                {
                    missingSymbols.Add(symbol);
                }
            }

            // For missing symbols, try to get from VNStock API (batch)
            if (missingSymbols.Any())
            {
                try
                {
                    var quotes = await _vnStockService.GetQuotesAsync(missingSymbols);
                    foreach (var quote in quotes)
                    {
                        if (quote != null)
                        {
                            result[quote.Symbol] = quote;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching quotes for missing symbols, will use individual lookups");
                    // Fallback to individual lookups for remaining symbols
                    foreach (var symbol in missingSymbols)
                    {
                        if (!result.ContainsKey(symbol))
                        {
                            var ticker = await GetTickerBySymbolAsync(symbol);
                            if (ticker != null)
                            {
                                result[symbol] = ticker;
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tickers by symbols");
            return new Dictionary<string, StockTicker>();
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

