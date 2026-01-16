using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.DTOs.Forecast;
using StockInvestment.Domain.Constants;
using System.Net;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ForecastController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly ITechnicalDataService _technicalDataService;
    private readonly ILogger<ForecastController> _logger;
    private readonly ICacheService _cacheService;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;

    public ForecastController(
        IAIService aiService,
        ITechnicalDataService technicalDataService,
        ILogger<ForecastController> logger,
        ICacheService cacheService,
        ICacheKeyGenerator cacheKeyGenerator)
    {
        _aiService = aiService;
        _technicalDataService = technicalDataService;
        _logger = logger;
        _cacheService = cacheService;
        _cacheKeyGenerator = cacheKeyGenerator;
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
            // Check cache first (cache for 4 hours to optimize quota usage)
            var cacheKey = _cacheKeyGenerator.GenerateForecastKey(symbol, timeHorizon);
            var cachedForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
            if (cachedForecast != null)
            {
                _logger.LogDebug("Returning cached forecast for {Symbol} with timeHorizon {TimeHorizon}", symbol, timeHorizon);
                return Ok(cachedForecast);
            }

            // Collect real technical indicators data
            var technicalData = await _technicalDataService.PrepareTechnicalDataAsync(symbol);

            // Try to get forecast with real data using POST endpoint
            ForecastResult? forecast = null;
            try
            {
                forecast = await _aiService.GenerateForecastWithDataAsync(symbol, timeHorizon, technicalData, null, null);
            }
            catch (HttpRequestException httpEx) when (ExtractStatusCode(httpEx) == 404)
            {
                _logger.LogWarning("AI service endpoint not found (404). Trying fallback GET endpoint.");
                try
                {
                    // Fallback to simple GET endpoint
                    forecast = await _aiService.GenerateForecastBySymbolAsync(symbol, timeHorizon);
                }
                catch (HttpRequestException httpEx2) when (ExtractStatusCode(httpEx2) == 404)
                {
                    _logger.LogWarning("Both AI service endpoints returned 404. AI service may not be running at configured URL.");
                    // Return service unavailable error
                    return StatusCode(503, new
                    {
                        error = "AI service unavailable",
                        message = "AI service không khả dụng hoặc không chạy. Vui lòng kiểm tra AI service đang chạy tại http://localhost:8000.",
                        symbol = symbol,
                        details = "Both /api/forecast/generate and /api/forecast/{symbol} endpoints returned 404"
                    });
                }
            }
            catch (HttpRequestException httpEx) when (ExtractStatusCode(httpEx) == 500)
            {
                var errorMessage = ExtractErrorMessage(httpEx);
                _logger.LogError(httpEx, "AI service returned 500 error (likely Gemini API issue) for {Symbol}", symbol);
                // Try fallback but don't expect it to work if it's a Gemini API issue
                try
                {
                    forecast = await _aiService.GenerateForecastBySymbolAsync(symbol, timeHorizon);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback endpoint also failed for {Symbol}", symbol);
                    return StatusCode(500, new
                    {
                        error = "AI service error",
                        message = $"Lỗi từ AI service: {errorMessage}",
                        symbol = symbol,
                        details = "This may be a Gemini API configuration issue. Please check AI service logs."
                    });
                }
            }
            catch (Exception aiEx)
            {
                var statusCode = ExtractStatusCode(aiEx);
                _logger.LogWarning(aiEx, "Failed to get forecast with real data (Status: {StatusCode}), trying fallback", statusCode);
                try
                {
                    // Fallback to simple GET endpoint
                    forecast = await _aiService.GenerateForecastBySymbolAsync(symbol, timeHorizon);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Both forecast endpoints failed for {Symbol}", symbol);
                    throw; // Re-throw to be handled by outer catch
                }
            }

            if (forecast == null)
            {
                // Return error response instead of mock data
                return StatusCode(503, new
                {
                    error = "AI service unavailable",
                    message = "Không thể kết nối đến AI service để phân tích. Vui lòng thử lại sau.",
                    symbol = symbol
                });
            }

            // Cache the forecast result for 4 hours
            await _cacheService.SetAsync(cacheKey, forecast, TimeSpan.FromHours(4));
            
            return Ok(forecast);
        }
        catch (HttpRequestException httpEx) when (ExtractStatusCode(httpEx) == 404)
        {
            _logger.LogError(httpEx, "AI service not found (404) for {Symbol}", symbol);
            return StatusCode(503, new
            {
                error = "AI service unavailable",
                message = "AI service không khả dụng hoặc không chạy. Vui lòng kiểm tra AI service đang chạy tại http://localhost:8000.",
                symbol = symbol
            });
        }
        catch (HttpRequestException httpEx) when (ExtractStatusCode(httpEx) == 429)
        {
            _logger.LogWarning(httpEx, "Quota exceeded (429) for {Symbol}", symbol);
            
            // Try to return cached forecast if available
            var cacheKey = $"forecast:{symbol}:{timeHorizon}";
            var cachedForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
            if (cachedForecast != null)
            {
                _logger.LogInformation("Returning cached forecast for {Symbol} due to quota exceeded", symbol);
                return Ok(cachedForecast);
            }
            
            return StatusCode(429, new
            {
                error = "Quota exceeded",
                message = "Đã vượt quá giới hạn quota Gemini API (20 requests/ngày). Vui lòng thử lại sau ít phút hoặc nâng cấp plan để tăng quota.",
                symbol = symbol,
                details = "Gemini API free tier có giới hạn 20 requests/ngày. Forecast được cache trong 4 giờ để tối ưu sử dụng quota."
            });
        }
        catch (HttpRequestException httpEx) when (ExtractStatusCode(httpEx) == 500)
        {
            var errorMessage = ExtractErrorMessage(httpEx);
            var isQuotaError = httpEx.Message.Contains("429") || httpEx.Message.Contains("quota") || httpEx.Message.Contains("Quota exceeded");
            
            _logger.LogError(httpEx, "AI service returned 500 error for {Symbol}. IsQuotaError: {IsQuotaError}", symbol, isQuotaError);
            
            // Check cache as fallback for quota errors
            if (isQuotaError)
            {
                var cacheKey = _cacheKeyGenerator.GenerateForecastKey(symbol, timeHorizon);
                var cachedForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
                if (cachedForecast != null)
                {
                    _logger.LogInformation("Returning cached forecast for {Symbol} due to quota exceeded", symbol);
                    return Ok(cachedForecast);
                }
            }
            
            return StatusCode(isQuotaError ? 429 : 500, new
            {
                error = isQuotaError ? "Quota exceeded" : "AI service error",
                message = isQuotaError 
                    ? "Đã vượt quá giới hạn quota Gemini API (20 requests/ngày). Vui lòng thử lại sau ít phút hoặc nâng cấp plan để tăng quota."
                    : $"Lỗi từ AI service: {errorMessage}",
                symbol = symbol,
                details = isQuotaError 
                    ? "Gemini API free tier có giới hạn 20 requests/ngày. Forecast được cache trong 4 giờ."
                    : "This may be a Gemini API configuration issue. Please check AI service logs."
            });
        }
        // Let GlobalExceptionHandlerMiddleware handle any remaining exceptions
    }

    private string GetRSILabel(decimal rsi)
    {
        if (rsi > TechnicalIndicatorConstants.RSI_OVERBOUGHT_THRESHOLD) return "Quá mua";
        if (rsi < TechnicalIndicatorConstants.RSI_OVERSOLD_THRESHOLD) return "Quá bán";
        return "Trung lập";
    }

    private int? ExtractStatusCode(Exception ex)
    {
        if (ex is HttpRequestException httpEx && httpEx.Data.Contains("StatusCode"))
        {
            return (int)httpEx.Data["StatusCode"]!;
        }
        return null;
    }

    private string ExtractErrorMessage(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            var message = httpEx.Message;
            
            // Check if it's a quota exceeded error (429)
            if (message.Contains("429") || message.Contains("quota") || message.Contains("Quota exceeded") || message.Contains("exceeded your current quota"))
            {
                return "Đã vượt quá giới hạn quota Gemini API (20 requests/ngày). Vui lòng thử lại sau hoặc nâng cấp plan.";
            }
            
            // Check if it's a Gemini API error
            if (message.Contains("gemini") || message.Contains("Gemini") || message.Contains("models/"))
            {
                // Check for specific quota error in detail
                if (message.Contains("429") || message.Contains("quota") || message.Contains("Quota exceeded"))
                {
                    return "Đã vượt quá giới hạn quota Gemini API (20 requests/ngày). Dự báo đã được cache, vui lòng thử lại sau ít phút.";
                }
                return "Lỗi Gemini API: " + message;
            }
            return message;
        }
        return ex.Message;
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

            return Ok(new ForecastResponseDto
            {
                Symbol = symbol,
                Forecasts = new ForecastTimeHorizonsDto
                {
                    ShortTerm = shortTerm,
                    MediumTerm = mediumTerm,
                    LongTerm = longTerm
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting forecasts for {Symbol}", symbol);
            throw; // Let GlobalExceptionHandlerMiddleware handle
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
            var forecasts = new List<ForecastItemDto>();

            foreach (var symbol in request.Symbols)
            {
                try
                {
                    var forecast = await _aiService.GenerateForecastBySymbolAsync(symbol, request.TimeHorizon);
                    forecasts.Add(new ForecastItemDto
                    {
                        Symbol = symbol,
                        Trend = forecast.Trend,
                        Confidence = forecast.Confidence,
                        Recommendation = forecast.Recommendation
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting forecast for {Symbol}", symbol);
                    forecasts.Add(new ForecastItemDto
                    {
                        Symbol = symbol,
                        Error = "Failed to generate forecast"
                    });
                }
            }

            return Ok(new BatchForecastResponseDto { Forecasts = forecasts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch forecasts");
            throw; // Let GlobalExceptionHandlerMiddleware handle
        }
    }
}

