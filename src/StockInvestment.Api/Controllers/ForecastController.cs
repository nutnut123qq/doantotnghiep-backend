using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ForecastController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly ILogger<ForecastController> _logger;

    public ForecastController(
        IAIService aiService,
        ILogger<ForecastController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Get AI forecast for a stock symbol
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetForecast(
        string symbol,
        [FromQuery] string timeHorizon = "short")
    {
        try
        {
            var forecast = await _aiService.GenerateForecastBySymbolAsync(symbol, timeHorizon);
            if (forecast == null)
            {
                // Return mock forecast when AI service is unavailable
                return Ok(new
                {
                    symbol = symbol,
                    trend = "Sideways",
                    confidence = "Medium",
                    confidenceScore = 50,
                    timeHorizon = timeHorizon,
                    recommendation = "Hold",
                    keyDrivers = new[] { "AI service đang khởi động", "Vui lòng thử lại sau" },
                    risks = new[] { "Dữ liệu chưa đầy đủ" },
                    analysis = "Đang kết nối với AI service để phân tích...",
                    generatedAt = DateTime.UtcNow.ToString("o")
                });
            }
            return Ok(forecast);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting forecast for {Symbol}", symbol);
            // Return mock data instead of 500 error
            return Ok(new
            {
                symbol = symbol,
                trend = "Sideways",
                confidence = "Low",
                confidenceScore = 30,
                timeHorizon = timeHorizon,
                recommendation = "Hold",
                keyDrivers = new[] { "Dịch vụ tạm thời không khả dụng" },
                risks = new[] { "Không thể kết nối AI service" },
                analysis = "Không thể tạo phân tích do lỗi kết nối.",
                generatedAt = DateTime.UtcNow.ToString("o")
            });
        }
    }

    /// <summary>
    /// Get multiple forecasts for different time horizons
    /// </summary>
    [HttpGet("{symbol}/all")]
    public async Task<IActionResult> GetAllForecasts(string symbol)
    {
        try
        {
            var shortTerm = await _aiService.GenerateForecastBySymbolAsync(symbol, "short");
            var mediumTerm = await _aiService.GenerateForecastBySymbolAsync(symbol, "medium");
            var longTerm = await _aiService.GenerateForecastBySymbolAsync(symbol, "long");

            return Ok(new
            {
                symbol,
                forecasts = new
                {
                    shortTerm,
                    mediumTerm,
                    longTerm
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all forecasts for {Symbol}", symbol);
            return StatusCode(500, "Error generating forecasts");
        }
    }

    /// <summary>
    /// Get forecast summary for multiple symbols
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> GetBatchForecasts([FromBody] BatchForecastRequest request)
    {
        try
        {
            var forecasts = new List<object>();

            foreach (var symbol in request.Symbols)
            {
                try
                {
                    var forecast = await _aiService.GenerateForecastBySymbolAsync(symbol, request.TimeHorizon);
                    forecasts.Add(new
                    {
                        symbol,
                        trend = forecast.Trend,
                        confidence = forecast.Confidence,
                        recommendation = forecast.Recommendation
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting forecast for {Symbol}", symbol);
                    forecasts.Add(new
                    {
                        symbol,
                        error = "Failed to generate forecast"
                    });
                }
            }

            return Ok(new { forecasts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch forecasts");
            return StatusCode(500, "Error generating batch forecasts");
        }
    }
}

public class BatchForecastRequest
{
    public List<string> Symbols { get; set; } = new();
    public string TimeHorizon { get; set; } = "short";
}

