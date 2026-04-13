using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using System.Diagnostics;

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

    public async Task<IEnumerable<StockTicker>> GetTickersAsync(
        string? exchange = null,
        string? index = null,
        string? industry = null,
        Guid? watchlistId = null,
        string? requestId = null)
    {
        var totalSw = Stopwatch.StartNew();
        var trace = string.IsNullOrWhiteSpace(requestId) ? "n/a" : requestId;
        try
        {
            _logger.LogInformation(
                "GetTickersAsync started requestId={RequestId} exchange={Exchange} index={Index} industry={Industry} watchlistId={WatchlistId}",
                trace,
                exchange ?? "null",
                index ?? "null",
                industry ?? "null",
                watchlistId?.ToString() ?? "null");

            // Nếu có watchlistId, lấy từ database
            if (watchlistId.HasValue)
            {
                var watchlistSw = Stopwatch.StartNew();
                var watchlist = await _watchlistRepository.GetByIdWithTickersAsync(watchlistId.Value);
                _logger.LogInformation(
                    "Phase watchlist lookup completed in {ElapsedMs}ms requestId={RequestId} found={Found}",
                    watchlistSw.ElapsedMilliseconds,
                    trace,
                    watchlist != null);

                if (watchlist != null)
                {
                    var wl = watchlist.Tickers.Where(t => Vn30Universe.Contains(t.Symbol)).ToList();
                    // Nếu có cả index filter, lấy intersection (chỉ VN30 được hỗ trợ)
                    if (!string.IsNullOrEmpty(index))
                    {
                        var wlIndexSymbols = IndexConstituentProvider.GetSymbols(index);
                        var filteredTickers = wl
                            .Where(t => wlIndexSymbols.Contains(t.Symbol, StringComparer.OrdinalIgnoreCase))
                            .ToList();
                        _logger.LogInformation(
                            "GetTickersAsync returning watchlist+index result count={Count} in {ElapsedMs}ms requestId={RequestId}",
                            filteredTickers.Count,
                            totalSw.ElapsedMilliseconds,
                            trace);
                        return filteredTickers;
                    }

                    _logger.LogInformation(
                        "GetTickersAsync returning watchlist result count={Count} in {ElapsedMs}ms requestId={RequestId}",
                        wl.Count,
                        totalSw.ElapsedMilliseconds,
                        trace);
                    return wl;
                }
            }

            // Board mặc định VN30 khi không chỉ định index (cùng cache key)
            var indexCacheSegment = string.IsNullOrWhiteSpace(index) ? "VN30" : index.Trim();
            var cacheKey = $"tickers_vnd_{exchange}_{indexCacheSegment}_{industry}";
            var cacheSw = Stopwatch.StartNew();
            var cachedTickers = await _cacheService.GetAsync<List<StockTicker>>(cacheKey);
            _logger.LogInformation(
                "Phase cache lookup completed in {ElapsedMs}ms requestId={RequestId} hit={Hit}",
                cacheSw.ElapsedMilliseconds,
                trace,
                cachedTickers != null);

            if (cachedTickers != null)
            {
                // Bỏ cache nếu toàn bộ mã không có giá hợp lệ (thường do lỗi deserialize/quote cũ).
                if (cachedTickers.Count > 0 && cachedTickers.TrueForAll(t => t.CurrentPrice <= 0))
                {
                    _logger.LogWarning(
                        "Discarding cached trading board: no valid prices requestId={RequestId} key={CacheKey}",
                        trace,
                        cacheKey);
                    await _cacheService.RemoveAsync(cacheKey);
                }
                else
                {
                    var filteredCache = cachedTickers.Where(t => Vn30Universe.Contains(t.Symbol)).ToList();
                    _logger.LogInformation(
                        "GetTickersAsync returning cache result count={Count} in {ElapsedMs}ms requestId={RequestId}",
                        filteredCache.Count,
                        totalSw.ElapsedMilliseconds,
                        trace);
                    return filteredCache;
                }
            }

            if (!string.IsNullOrWhiteSpace(index)
                && !string.Equals(index.Trim(), "VN30", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unsupported index {Index}; only VN30 is allowed", index);
                return Enumerable.Empty<StockTicker>();
            }

            // TradingBoard is backend-driven:
            // always serve from cache/database populated by StockPriceUpdateJob,
            // never trigger VNStock fetch from request path.
            var dbSw = Stopwatch.StartNew();
            var dbTickers = await _stockTickerRepository.GetTickersAsync(exchange, null, industry);
            var dbTickersList = dbTickers.Where(t => Vn30Universe.Contains(t.Symbol)).ToList();
            _logger.LogInformation(
                "Phase db lookup completed in {ElapsedMs}ms requestId={RequestId} count={Count}",
                dbSw.ElapsedMilliseconds,
                trace,
                dbTickersList.Count);

            if (dbTickersList.Any())
            {
                // Cache 2 phút
                await _cacheService.SetAsync(cacheKey, dbTickersList, TimeSpan.FromMinutes(2));

                _logger.LogInformation(
                    "GetTickersAsync returning db result count={Count} in {ElapsedMs}ms requestId={RequestId}",
                    dbTickersList.Count,
                    totalSw.ElapsedMilliseconds,
                    trace);
                return dbTickersList;
            }

            _logger.LogInformation(
                "GetTickersAsync returning empty result (no db/cache data yet) in {ElapsedMs}ms requestId={RequestId}",
                totalSw.ElapsedMilliseconds,
                trace);
            return Enumerable.Empty<StockTicker>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tickers requestId={RequestId} after {ElapsedMs}ms", trace, totalSw.ElapsedMilliseconds);
            // Fallback to mock data
            return GenerateMockTickers();
        }
    }

    public async Task<StockTicker?> GetTickerBySymbolAsync(string symbol)
    {
        try
        {
            var normalizedSymbol = symbol.Trim().ToUpperInvariant();

            if (!Vn30Universe.Contains(normalizedSymbol))
            {
                return await _stockTickerRepository.GetBySymbolAsync(normalizedSymbol);
            }

            var cacheKey = $"ticker_vnd_{normalizedSymbol}";
            var cachedTicker = await _cacheService.GetAsync<StockTicker>(cacheKey);

            if (cachedTicker != null)
            {
                return NormalizeTickerUnitIfNeeded(cachedTicker);
            }

            // Prefer local DB snapshot first to avoid blocking user requests
            // when the external VNStock API is slow or temporarily unavailable.
            var dbTicker = await _stockTickerRepository.GetBySymbolAsync(normalizedSymbol);
            if (dbTicker != null)
            {
                var normalizedDbTicker = NormalizeTickerUnitIfNeeded(dbTicker);
                await _cacheService.SetAsync(cacheKey, normalizedDbTicker, TimeSpan.FromMinutes(1));
                return normalizedDbTicker;
            }

            var ticker = await _vnStockService.GetQuoteAsync(normalizedSymbol);

            if (ticker != null)
            {
                ticker = NormalizeTickerUnitIfNeeded(ticker);
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
                result[kvp.Key] = NormalizeTickerUnitIfNeeded(kvp.Value);
            }

            foreach (var symbol in symbolsList)
            {
                if (!result.ContainsKey(symbol))
                {
                    missingSymbols.Add(symbol);
                }
            }

            var missingVn30 = missingSymbols.Where(Vn30Universe.Contains).ToList();

            if (missingVn30.Any())
            {
                try
                {
                    var quotes = await _vnStockService.GetQuotesAsync(missingVn30);
                    foreach (var quote in quotes)
                    {
                        if (quote != null)
                        {
                            result[quote.Symbol] = NormalizeTickerUnitIfNeeded(quote);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching quotes for missing VN30 symbols, will use individual lookups");
                    foreach (var sym in missingVn30)
                    {
                        if (!result.ContainsKey(sym))
                        {
                            var ticker = await GetTickerBySymbolAsync(sym);
                            if (ticker != null)
                            {
                                result[sym] = ticker;
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

    private StockTicker NormalizeTickerUnitIfNeeded(StockTicker ticker)
    {
        if (ticker.CurrentPrice <= 0 || ticker.CurrentPrice >= 1000m)
        {
            return ticker;
        }

        var normalized = new StockTicker
        {
            Id = ticker.Id,
            Symbol = ticker.Symbol,
            Name = ticker.Name,
            Exchange = ticker.Exchange,
            Industry = ticker.Industry,
            CurrentPrice = ticker.CurrentPrice * 1000m,
            PreviousClose = ticker.PreviousClose.HasValue ? ticker.PreviousClose * 1000m : null,
            Change = ticker.Change.HasValue ? ticker.Change * 1000m : null,
            ChangePercent = ticker.ChangePercent,
            Volume = ticker.Volume,
            Value = ticker.Value.HasValue ? ticker.Value * 1000m : null,
            LastUpdated = ticker.LastUpdated
        };

        _logger.LogWarning(
            "Detected legacy thousand-VND ticker unit for {Symbol}. Auto-normalized current price from {CurrentPrice} to {NormalizedPrice}",
            ticker.Symbol,
            ticker.CurrentPrice,
            normalized.CurrentPrice);

        return normalized;
    }
}

