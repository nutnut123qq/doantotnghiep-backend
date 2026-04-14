using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.DTOs.StockData;
using StockInvestment.Domain.Constants;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockDataController : ControllerBase
{
    private readonly IStockTickerRepository _stockTickerRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<StockDataController> _logger;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;
    private readonly TimeSpan _quoteCacheDuration;
    private readonly TimeSpan _quoteStaleThreshold;
    private readonly TimeSpan _ohlcvStaleThreshold;

    public StockDataController(
        IStockTickerRepository stockTickerRepository,
        ICacheService cacheService,
        ILogger<StockDataController> logger,
        ICacheKeyGenerator cacheKeyGenerator,
        IConfiguration configuration)
    {
        _stockTickerRepository = stockTickerRepository;
        _cacheService = cacheService;
        _logger = logger;
        _cacheKeyGenerator = cacheKeyGenerator;
        _quoteCacheDuration = BuildDuration(configuration.GetValue("Features:QuoteCacheMinutes", 1), 1, 15);
        _quoteStaleThreshold = BuildDuration(configuration.GetValue("Features:QuoteStaleMinutes", 5), 1, 120);
        _ohlcvStaleThreshold = BuildDuration(configuration.GetValue("Features:OHLCVStaleMinutes", 60), 5, 10080);
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
            if (!Vn30Universe.Contains(symbol))
            {
                return NotFound(new { message = $"OHLCV is only available for VN30 symbols. '{symbol}' is not supported." });
            }

            var start = startDate ?? DateTime.Now.AddMonths(-3);
            var end = endDate ?? DateTime.Now;
            var cacheKey = _cacheKeyGenerator.GenerateOHLCVKey(symbol, start, end);

            // Read-only request path: do not call VNStock from HTTP requests.
            var ohlcvData = await _cacheService.GetAsync<List<OHLCVResponseDto>>(cacheKey);
            if (ohlcvData == null || ohlcvData.Count == 0)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = $"Historical data for {symbol} is warming up. Try again shortly."
                });
            }

            var lastUpdated = DateTimeOffset
                .FromUnixTimeSeconds(ohlcvData.Max(i => i.Time))
                .UtcDateTime;
            var isStale = DateTime.UtcNow - lastUpdated > _ohlcvStaleThreshold;
            SetDataFreshnessHeaders(isStale, lastUpdated, "cache");

            return Ok(ohlcvData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OHLCV data for {Symbol}", symbol);
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }

    /// <summary>
    /// Get latest quote for a symbol
    /// </summary>
    [HttpGet("quote/{symbol}")]
    public async Task<IActionResult> GetQuote(string symbol)
    {
        try
        {
            if (!Vn30Universe.Contains(symbol))
            {
                return NotFound(new { message = $"Quote is only available for VN30 symbols. '{symbol}' is not supported." });
            }

            var cacheKey = _cacheKeyGenerator.GenerateQuoteKey(symbol);

            // Try to get from cache first
            var cachedQuote = await _cacheService.GetAsync<QuoteResponseDto>(cacheKey);
            if (cachedQuote != null)
            {
                cachedQuote.DataSource = "cache";
                cachedQuote.IsStale = DateTime.UtcNow - cachedQuote.LastUpdated > _quoteStaleThreshold;
                SetDataFreshnessHeaders(cachedQuote.IsStale, cachedQuote.LastUpdated, cachedQuote.DataSource);
                return Ok(cachedQuote);
            }

            // Read-only request path: fallback to DB snapshot.
            var dbQuote = await _stockTickerRepository.GetBySymbolAsync(symbol.Trim().ToUpperInvariant());
            if (dbQuote == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = $"Quote for {symbol} is warming up. Try again shortly."
                });
            }

            var quoteDto = new QuoteResponseDto
            {
                Symbol = dbQuote.Symbol,
                Name = dbQuote.Name,
                Exchange = dbQuote.Exchange.ToString(),
                CurrentPrice = dbQuote.CurrentPrice,
                PreviousClose = dbQuote.PreviousClose ?? 0,
                Change = dbQuote.Change ?? 0,
                ChangePercent = dbQuote.ChangePercent ?? 0,
                Volume = dbQuote.Volume ?? 0,
                LastUpdated = dbQuote.LastUpdated,
                DataSource = "db",
            };
            quoteDto.IsStale = DateTime.UtcNow - quoteDto.LastUpdated > _quoteStaleThreshold;

            await _cacheService.SetAsync(cacheKey, quoteDto, _quoteCacheDuration);
            SetDataFreshnessHeaders(quoteDto.IsStale, quoteDto.LastUpdated, quoteDto.DataSource);

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
            var dict = await _stockTickerRepository.GetBySymbolsAsync(Vn30Universe.Symbols);
            var symbolList = new List<object>();
            foreach (var s in Vn30Universe.Symbols)
            {
                dict.TryGetValue(s, out var t);
                var exchangeStr = t?.Exchange.ToString() ?? "HOSE";
                if (!string.IsNullOrEmpty(exchange)
                    && !exchangeStr.Equals(exchange, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                symbolList.Add(new
                {
                    symbol = s,
                    name = t?.Name ?? s,
                    exchange = exchangeStr,
                    industry = t?.Industry
                });
            }

            return Ok(symbolList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting symbols");
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }

    private static TimeSpan BuildDuration(int minutes, int minMinutes, int maxMinutes)
    {
        var bounded = Math.Clamp(minutes, minMinutes, maxMinutes);
        return TimeSpan.FromMinutes(bounded);
    }

    private void SetDataFreshnessHeaders(bool isStale, DateTime lastUpdated, string source)
    {
        Response.Headers["X-Data-Stale"] = isStale ? "true" : "false";
        Response.Headers["X-Data-Source"] = source;
        Response.Headers["X-Data-LastUpdated"] = lastUpdated.ToUniversalTime().ToString("O");
    }

}

