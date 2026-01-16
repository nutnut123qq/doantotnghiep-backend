using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TechnicalIndicatorController : ControllerBase
{
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<TechnicalIndicatorController> _logger;

    public TechnicalIndicatorController(
        ITechnicalIndicatorService indicatorService,
        ILogger<TechnicalIndicatorController> logger)
    {
        _indicatorService = indicatorService;
        _logger = logger;
    }

    /// <summary>
    /// Get all technical indicators for a symbol
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetIndicators(string symbol)
    {
        try
        {
            var indicators = await _indicatorService.CalculateAllIndicatorsAsync(symbol);
            return Ok(new { symbol, indicators });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating indicators for {Symbol}", symbol);
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }

    /// <summary>
    /// Get specific indicator (MA, RSI, MACD)
    /// </summary>
    [HttpGet("{symbol}/{indicatorType}")]
    public async Task<IActionResult> GetIndicator(string symbol, string indicatorType, [FromQuery] int period = 14)
    {
        try
        {
            object? result = indicatorType.ToUpper() switch
            {
                "MA" => await _indicatorService.CalculateMAAsync(symbol, period),
                "RSI" => await _indicatorService.CalculateRSIAsync(symbol, period),
                "MACD" => await _indicatorService.CalculateMACDAsync(symbol),
                _ => null
            };

            if (result == null)
            {
                return BadRequest("Invalid indicator type. Use MA, RSI, or MACD");
            }

            return Ok(new { symbol, indicatorType, value = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating {IndicatorType} for {Symbol}", indicatorType, symbol);
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }
}

