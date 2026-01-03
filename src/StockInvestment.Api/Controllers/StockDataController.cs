using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockDataController : ControllerBase
{
    private readonly IVNStockService _vnStockService;
    private readonly ILogger<StockDataController> _logger;

    public StockDataController(
        IVNStockService vnStockService,
        ILogger<StockDataController> logger)
    {
        _vnStockService = vnStockService;
        _logger = logger;
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

            var data = await _vnStockService.GetHistoricalDataAsync(symbol, start, end);
            
            // If no data from external service, generate mock data for demo
            if (data == null || !data.Any())
            {
                _logger.LogWarning("No historical data from external service for {Symbol}, using mock data", symbol);
                data = GenerateMockHistoricalData(symbol, start, end);
            }
            
            var ohlcvData = data.Select(d => new
            {
                time = new DateTimeOffset(d.Date).ToUnixTimeSeconds(),
                open = d.Open,
                high = d.High,
                low = d.Low,
                close = d.Close,
                volume = d.Volume
            }).OrderBy(d => d.time);

            return Ok(ohlcvData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OHLCV data for {Symbol}", symbol);
            return StatusCode(500, "Error fetching chart data");
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
            var quote = await _vnStockService.GetQuoteAsync(symbol);
            if (quote == null)
            {
                return NotFound($"Symbol {symbol} not found");
            }

            return Ok(new
            {
                symbol = quote.Symbol,
                name = quote.Name,
                exchange = quote.Exchange.ToString(),
                currentPrice = quote.CurrentPrice,
                previousClose = quote.PreviousClose,
                change = quote.Change,
                changePercent = quote.ChangePercent,
                volume = quote.Volume,
                lastUpdated = quote.LastUpdated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quote for {Symbol}", symbol);
            return StatusCode(500, "Error fetching quote");
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
            return StatusCode(500, "Error fetching symbols");
        }
    }
}

