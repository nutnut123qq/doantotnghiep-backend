using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TechnicalIndicatorController : ControllerBase
{
    private readonly ITechnicalIndicatorQueryService _indicatorQueryService;
    private readonly ILogger<TechnicalIndicatorController> _logger;
    private readonly TimeSpan _indicatorStaleThreshold;

    public TechnicalIndicatorController(
        ITechnicalIndicatorQueryService indicatorQueryService,
        ILogger<TechnicalIndicatorController> logger,
        IConfiguration configuration)
    {
        _indicatorQueryService = indicatorQueryService;
        _logger = logger;
        var staleMinutes = configuration.GetValue("Features:IndicatorStaleMinutes", 120);
        _indicatorStaleThreshold = TimeSpan.FromMinutes(Math.Clamp(staleMinutes, 5, 10080));
    }

    /// <summary>
    /// Get all technical indicators for a symbol from stored data (read-only request path).
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetIndicators(string symbol, [FromQuery] bool live = false)
    {
        try
        {
            if (!Vn30Universe.Contains(symbol))
            {
                return NotFound(new { message = $"Technical indicators are only available for VN30 symbols. '{symbol}' is not supported." });
            }

            var stored = await _indicatorQueryService.GetLatestStoredIndicatorsAsync(symbol);
            if (stored.Count == 0)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = $"Indicators for {symbol} are warming up. Try again shortly."
                });
            }

            var lastUpdated = stored.Max(i => i.CalculatedAt);
            var isStale = DateTime.UtcNow - lastUpdated > _indicatorStaleThreshold;
            return Ok(new
            {
                symbol,
                indicators = stored,
                isStale,
                lastUpdated,
                source = "db"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting indicators for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Get specific indicator (MA, RSI, MACD) from stored data (read-only request path).
    /// </summary>
    [HttpGet("{symbol}/{indicatorType}")]
    public async Task<IActionResult> GetIndicator(
        string symbol,
        string indicatorType,
        [FromQuery] int period = 14,
        [FromQuery] bool live = false)
    {
        try
        {
            if (!Vn30Universe.Contains(symbol))
            {
                return NotFound(new { message = $"Technical indicators are only available for VN30 symbols. '{symbol}' is not supported." });
            }

            if (live)
            {
                return BadRequest(new
                {
                    message = "Live indicator calculation is disabled on request path. Please use stored indicators."
                });
            }

            var stored = await _indicatorQueryService.GetLatestStoredIndicatorsAsync(symbol);
            if (stored.Count == 0)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = $"Indicators for {symbol} are warming up. Try again shortly."
                });
            }

            var row = FindStoredRow(stored, indicatorType, period);
            if (row == null)
            {
                return NotFound(new { message = $"Stored indicator not found for {indicatorType} (period={period})." });
            }

            object valuePayload = indicatorType.ToUpperInvariant() switch
            {
                "MACD" => new MACDResult
                {
                    MACD = row.Value ?? 0,
                    Signal = 0,
                    Histogram = 0
                },
                _ => row.Value ?? 0m
            };

            var lastUpdated = row.CalculatedAt;
            var isStale = DateTime.UtcNow - lastUpdated > _indicatorStaleThreshold;
            return Ok(new
            {
                symbol,
                indicatorType,
                value = valuePayload,
                isStale,
                lastUpdated,
                source = "db"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting {IndicatorType} for {Symbol}", indicatorType, symbol);
            throw;
        }
    }

    private static TechnicalIndicator? FindStoredRow(
        IReadOnlyList<TechnicalIndicator> stored,
        string indicatorType,
        int period)
    {
        return indicatorType.ToUpperInvariant() switch
        {
            "RSI" => stored.FirstOrDefault(i => string.Equals(i.IndicatorType, "RSI", StringComparison.OrdinalIgnoreCase)),
            "MACD" => stored.FirstOrDefault(i => string.Equals(i.IndicatorType, "MACD", StringComparison.OrdinalIgnoreCase)),
            "MA" when period >= 45 => stored.FirstOrDefault(i =>
                string.Equals(i.IndicatorType, "MA50", StringComparison.OrdinalIgnoreCase)),
            "MA" => stored.FirstOrDefault(i =>
                string.Equals(i.IndicatorType, "MA20", StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }
}
