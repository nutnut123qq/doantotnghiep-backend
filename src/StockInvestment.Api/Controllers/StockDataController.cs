using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.DTOs.StockData;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockDataController : ControllerBase
{
    private readonly IVNStockService _vnStockService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<StockDataController> _logger;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;

    public StockDataController(
        IVNStockService vnStockService,
        ICacheService cacheService,
        ILogger<StockDataController> logger,
        ICacheKeyGenerator cacheKeyGenerator)
    {
        _vnStockService = vnStockService;
        _cacheService = cacheService;
        _logger = logger;
        _cacheKeyGenerator = cacheKeyGenerator;
    }

    /// <summary>
    /// Get OHLCV historical data for TradingView charts
    /// </summary>
    [HttpGet("ohlcv/{symbol}")]
    public async Task<IActionResult> GetOHLCV(
        string symbol,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Now.AddMonths(-3);
            var end = endDate ?? DateTime.Now;
            var cacheKey = _cacheKeyGenerator.GenerateOHLCVKey(symbol, start, end);

            // Get from cache or fetch and cache
            var ohlcvData = await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug("Cache miss for OHLCV data: {CacheKey}, fetching from service", cacheKey);
                    
                    var data = await _vnStockService.GetHistoricalDataAsync(symbol, start, end);
                    
                    // If no data from external service, generate mock data for demo
                    if (data == null || !data.Any())
                    {
                        _logger.LogWarning("No historical data from external service for {Symbol}, using mock data", symbol);
                        data = GenerateMockHistoricalData(symbol, start, end);
                    }
                    
                    return data.Select(d => new OHLCVResponseDto
                    {
                        Time = new DateTimeOffset(d.Date).ToUnixTimeSeconds(),
                        Open = d.Open,
                        High = d.High,
                        Low = d.Low,
                        Close = d.Close,
                        Volume = d.Volume
                    }).OrderBy(d => d.Time).ToList();
                },
                TimeSpan.FromMinutes(30)
            );

            return Ok(ohlcvData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OHLCV data for {Symbol}", symbol);
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }
    
    private List<OHLCVData> GenerateMockHistoricalData(string symbol, DateTime start, DateTime end)
    {
        var data = new List<OHLCVData>();
        var random = new Random(symbol.GetHashCode());
        var basePrice = random.Next(50000, 200000) / 1000.0m; // Random base price between 50-200
        var currentPrice = basePrice;
        
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;
                
            var changePercent = (decimal)((random.NextDouble() - 0.5) * 0.06); // +/- 3% daily change
            var open = currentPrice;
            var close = open * (1 + changePercent);
            var high = Math.Max(open, close) * (1 + (decimal)(random.NextDouble() * 0.02));
            var low = Math.Min(open, close) * (1 - (decimal)(random.NextDouble() * 0.02));
            var volume = random.Next(100000, 10000000);
            
            data.Add(new OHLCVData
            {
                Date = date,
                Open = Math.Round(open, 2),
                High = Math.Round(high, 2),
                Low = Math.Round(low, 2),
                Close = Math.Round(close, 2),
                Volume = volume
            });
            
            currentPrice = close;
        }
        
        return data;
    }

    /// <summary>
    /// Get latest quote for a symbol
    /// </summary>
    [HttpGet("quote/{symbol}")]
    public async Task<IActionResult> GetQuote(string symbol)
    {
        try
        {
            var cacheKey = _cacheKeyGenerator.GenerateQuoteKey(symbol);

            // Try to get from cache first
            var cachedQuote = await _cacheService.GetAsync<QuoteResponseDto>(cacheKey);
            if (cachedQuote != null)
            {
                return Ok(cachedQuote);
            }

            // Cache miss - fetch from service
            _logger.LogDebug("Cache miss for quote data: {CacheKey}, fetching from service", cacheKey);
            
            var quote = await _vnStockService.GetQuoteAsync(symbol);
            if (quote == null)
            {
                return NotFound(new { message = $"Quote not found for symbol: {symbol}" });
            }

            var quoteDto = new QuoteResponseDto
            {
                Symbol = quote.Symbol,
                Name = quote.Name,
                Exchange = quote.Exchange.ToString(),
                CurrentPrice = quote.CurrentPrice,
                PreviousClose = quote.PreviousClose ?? 0,
                Change = quote.Change ?? 0,
                ChangePercent = quote.ChangePercent ?? 0,
                Volume = quote.Volume ?? 0,
                LastUpdated = quote.LastUpdated
            };

            // Cache the result (5 minutes expiration for real-time data)
            await _cacheService.SetAsync(cacheKey, quoteDto, TimeSpan.FromMinutes(5));

            return Ok(quoteDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quote for {Symbol}", symbol);
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }

    /// <summary>
    /// Get all available symbols
    /// </summary>
    [HttpGet("symbols")]
    public async Task<IActionResult> GetSymbols([FromQuery] string? exchange = null)
    {
        try
        {
            var symbols = await _vnStockService.GetAllSymbolsAsync(exchange);
            var symbolList = symbols.Select(s => new
            {
                symbol = s.Symbol,
                name = s.Name,
                exchange = s.Exchange.ToString(),
                industry = s.Industry
            });

            return Ok(symbolList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting symbols");
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }

}

