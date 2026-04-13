using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using System.Diagnostics;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradingBoardController : ControllerBase
{
    private readonly IStockDataService _stockDataService;
    private readonly ILogger<TradingBoardController> _logger;

    public TradingBoardController(IStockDataService stockDataService, ILogger<TradingBoardController> logger)
    {
        _stockDataService = stockDataService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetTickers(
        [FromQuery] string? exchange = null,
        [FromQuery] string? index = null,
        [FromQuery] string? industry = null,
        [FromQuery] Guid? watchlistId = null)
    {
        var requestId = HttpContext.TraceIdentifier;
        var sw = Stopwatch.StartNew();
        try
        {
            if (!string.IsNullOrWhiteSpace(index)
                && !string.Equals(index.Trim(), "VN30", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only VN30 index is supported." });
            }

            var tickers = await _stockDataService.GetTickersAsync(exchange, index, industry, watchlistId, requestId);
            _logger.LogInformation(
                "TradingBoard request completed in {ElapsedMs}ms requestId={RequestId}",
                sw.ElapsedMilliseconds,
                requestId);
            return Ok(tickers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tickers requestId={RequestId}", requestId);
            return StatusCode(500, "An error occurred while fetching tickers");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTickerById(Guid id)
    {
        try
        {
            var ticker = await _stockDataService.GetTickerByIdAsync(id);
            if (ticker == null)
                return NotFound();

            return Ok(ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticker {Id}", id);
            return StatusCode(500, "An error occurred while fetching ticker");
        }
    }
}

