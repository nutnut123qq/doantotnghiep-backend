using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TechnicalIndicatorController : ControllerBase
{
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ITechnicalIndicatorQueryService _indicatorQueryService;
    private readonly ILogger<TechnicalIndicatorController> _logger;

    public TechnicalIndicatorController(
        ITechnicalIndicatorService indicatorService,
        ITechnicalIndicatorQueryService indicatorQueryService,
        ILogger<TechnicalIndicatorController> logger)
    {
        _indicatorService = indicatorService;
        _indicatorQueryService = indicatorQueryService;
        _logger = logger;
    }

    /// <summary>
    /// Get all technical indicators for a symbol (from DB by default; use live=true to recalculate via market data).
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

            if (live)
            {
                var calculated = await _indicatorService.CalculateAllIndicatorsAsync(symbol);
                return Ok(new { symbol, indicators = calculated });
            }

            var stored = await _indicatorQueryService.GetLatestStoredIndicatorsAsync(symbol);
            return Ok(new { symbol, indicators = stored });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting indicators for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Get specific indicator (MA, RSI, MACD). From DB when live=false; recalculates when live=true.
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
                object? calculated = indicatorType.ToUpperInvariant() switch
                {
                    "MA" => await _indicatorService.CalculateMAAsync(symbol, period),
                    "RSI" => await _indicatorService.CalculateRSIAsync(symbol, period),
                    "MACD" => await _indicatorService.CalculateMACDAsync(symbol),
                    _ => null
                };

                if (calculated == null)
                {
                    return BadRequest("Invalid indicator type. Use MA, RSI, or MACD");
                }

                return Ok(new { symbol, indicatorType, value = calculated });
            }

            var stored = await _indicatorQueryService.GetLatestStoredIndicatorsAsync(symbol);
            if (stored.Count == 0)
            {
                return NotFound(new { message = $"No stored indicators for {symbol}. Wait for the technical indicator job or pass live=true." });
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

            return Ok(new { symbol, indicatorType, value = valuePayload });
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
