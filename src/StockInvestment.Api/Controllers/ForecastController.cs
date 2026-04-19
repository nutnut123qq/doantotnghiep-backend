using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Configuration;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.DTOs.Forecast;
using StockInvestment.Application.DTOs.LangGraph;
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
    private readonly IOptions<StockAnalystOptions> _stockAnalystOptions;
    private readonly ILangGraphForecastClient _langGraphForecastClient;
    private readonly ILangGraphForecastMapper _langGraphForecastMapper;

    public ForecastController(
        IAIService aiService,
        ITechnicalDataService technicalDataService,
        ILogger<ForecastController> logger,
        ICacheService cacheService,
        ICacheKeyGenerator cacheKeyGenerator,
        IOptions<StockAnalystOptions> stockAnalystOptions,
        ILangGraphForecastClient langGraphForecastClient,
        ILangGraphForecastMapper langGraphForecastMapper)
    {
        _aiService = aiService;
        _technicalDataService = technicalDataService;
        _logger = logger;
        _cacheService = cacheService;
        _cacheKeyGenerator = cacheKeyGenerator;
        _stockAnalystOptions = stockAnalystOptions;
        _langGraphForecastClient = langGraphForecastClient;
        _langGraphForecastMapper = langGraphForecastMapper;
    }

    /// <summary>
    /// Get AI forecast for a stock symbol
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetForecast(
        string symbol,
        [FromQuery] string timeHorizon = "short",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_stockAnalystOptions.Value.Enabled)
            {
                return await GetForecastViaLangGraphAsync(symbol, timeHorizon, cancellationToken);
            }

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

    private async Task<IActionResult> GetForecastViaLangGraphAsync(
        string symbol,
        string timeHorizon,
        CancellationToken cancellationToken)
    {
        var cacheKey = _cacheKeyGenerator.GenerateLangGraphForecastKey(symbol, timeHorizon);
        var cached = await _cacheService.GetAsync<ForecastResult>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug(
                "Returning cached LangGraph forecast for {Symbol} timeHorizon {TimeHorizon}",
                symbol,
                timeHorizon);
            return Ok(cached);
        }

        try
        {
            var job = await _langGraphForecastClient.EnqueueAnalyzeAsync(symbol, cancellationToken);
            if (job == null)
            {
                return StatusCode(503, new
                {
                    error = "Stock analyst unavailable",
                    message = "Dịch vụ phân tích LangGraph không phản hồi khi tạo job. Vui lòng thử lại sau.",
                    symbol
                });
            }

            // Python returned an inline cache hit — short-circuit to the mapped result.
            if (string.Equals(job.Status, "completed", StringComparison.OrdinalIgnoreCase)
                && job.Result != null)
            {
                var forecast = _langGraphForecastMapper.Map(job.Result, symbol, timeHorizon);
                await _cacheService.SetAsync(cacheKey, forecast, TimeSpan.FromHours(8));
                return Ok(forecast);
            }

            if (string.IsNullOrWhiteSpace(job.JobId))
            {
                _logger.LogWarning(
                    "LangGraph enqueue returned no jobId for {Symbol} (status={Status})",
                    symbol,
                    job.Status);
                return StatusCode(503, new
                {
                    error = "Stock analyst unavailable",
                    message = "Dịch vụ phân tích LangGraph không trả về jobId. Vui lòng thử lại sau.",
                    symbol
                });
            }

            _logger.LogInformation(
                "Enqueued LangGraph forecast job {JobId} for {Symbol} (status={Status})",
                job.JobId,
                symbol,
                job.Status);

            return StatusCode(202, new
            {
                status = (job.Status ?? "queued").ToLowerInvariant(),
                jobId = job.JobId,
                symbol,
                timeHorizon
            });
        }
        catch (TaskCanceledException tcEx) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(tcEx, "LangGraph enqueue timed out for {Symbol}", symbol);
            var staleForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
            if (staleForecast != null)
            {
                _logger.LogWarning("Returning stale LangGraph cache for {Symbol} due to enqueue timeout", symbol);
                return Ok(staleForecast);
            }

            return StatusCode(504, new
            {
                error = "Stock analyst timeout",
                message = "Dịch vụ phân tích LangGraph không phản hồi khi tạo job. Vui lòng thử lại sau ít phút.",
                symbol
            });
        }
        catch (HttpRequestException httpEx)
        {
            var status = ExtractStatusCode(httpEx);
            _logger.LogError(httpEx, "LangGraph enqueue HTTP error for {Symbol} status {Status}", symbol, status);

            if (status >= 500 || status == null)
            {
                var staleForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
                if (staleForecast != null)
                {
                    _logger.LogWarning(
                        "Returning stale LangGraph cache for {Symbol} due to enqueue 5xx ({Status})",
                        symbol,
                        status);
                    return Ok(staleForecast);
                }
            }

            return StatusCode(503, new
            {
                error = "Stock analyst unavailable",
                message = $"Không gọi được dịch vụ LangGraph ({status}): {httpEx.Message}",
                symbol
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LangGraph enqueue failed for {Symbol}", symbol);
            var staleForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
            if (staleForecast != null)
            {
                _logger.LogWarning("Returning stale LangGraph cache for {Symbol} after enqueue exception", symbol);
                return Ok(staleForecast);
            }

            return StatusCode(503, new
            {
                error = "Stock analyst error",
                message = "Lỗi khi tạo job phân tích LangGraph. Vui lòng thử lại sau.",
                symbol,
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Poll the status of an enqueued LangGraph forecast job.
    /// Returns the mapped <see cref="ForecastResult"/> once Python reports <c>completed</c>,
    /// HTTP 202 while the job is still running, or an error payload on failure.
    /// </summary>
    [HttpGet("langgraph/jobs/{jobId}")]
    public async Task<IActionResult> GetLangGraphJob(
        string jobId,
        [FromQuery] string symbol,
        [FromQuery] string timeHorizon = "short",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest(new { error = "jobId is required" });
        }
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new { error = "symbol query param is required" });
        }

        var cacheKey = _cacheKeyGenerator.GenerateLangGraphForecastKey(symbol, timeHorizon);

        LangGraphJobResponse? job;
        try
        {
            job = await _langGraphForecastClient.GetAnalyzeJobAsync(jobId, cancellationToken);
        }
        catch (TaskCanceledException tcEx) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(tcEx, "LangGraph poll timed out for job {JobId}", jobId);
            return StatusCode(504, new
            {
                error = "Stock analyst timeout",
                message = "Không lấy được trạng thái job LangGraph. Vui lòng thử lại sau.",
                jobId
            });
        }
        catch (HttpRequestException httpEx)
        {
            var status = ExtractStatusCode(httpEx);
            _logger.LogError(httpEx, "LangGraph poll HTTP error for job {JobId} ({Status})", jobId, status);

            if (status >= 500 || status == null)
            {
                var staleForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
                if (staleForecast != null)
                {
                    _logger.LogWarning("Returning stale LangGraph cache for {Symbol} due to poll 5xx", symbol);
                    return Ok(staleForecast);
                }
            }

            return StatusCode(503, new
            {
                error = "Stock analyst unavailable",
                message = $"Không gọi được dịch vụ LangGraph ({status}): {httpEx.Message}",
                jobId
            });
        }

        if (job == null)
        {
            // Python returned 404 — job expired. Fall back to stale cache if we have one.
            var staleForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
            if (staleForecast != null)
            {
                _logger.LogInformation("LangGraph job {JobId} expired; returning stale cache for {Symbol}", jobId, symbol);
                return Ok(staleForecast);
            }

            return StatusCode(404, new
            {
                error = "Job not found",
                message = "Job phân tích LangGraph không còn tồn tại. Vui lòng bấm Thử lại để tạo lần mới.",
                jobId
            });
        }

        var jobStatus = (job.Status ?? string.Empty).ToLowerInvariant();

        if (jobStatus == "completed" && job.Result != null)
        {
            var forecast = _langGraphForecastMapper.Map(job.Result, symbol, timeHorizon);
            await _cacheService.SetAsync(cacheKey, forecast, TimeSpan.FromHours(8));
            return Ok(forecast);
        }

        if (jobStatus == "failed")
        {
            _logger.LogWarning(
                "LangGraph job {JobId} failed for {Symbol}: {Error}",
                jobId,
                symbol,
                job.Error);

            var staleForecast = await _cacheService.GetAsync<ForecastResult>(cacheKey);
            if (staleForecast != null)
            {
                _logger.LogWarning("Returning stale LangGraph cache for {Symbol} after job failure", symbol);
                return Ok(staleForecast);
            }

            return StatusCode(500, new
            {
                error = "Stock analyst failed",
                message = "Phân tích LangGraph gặp lỗi. Vui lòng thử lại sau.",
                jobId,
                details = job.Error
            });
        }

        return StatusCode(202, new
        {
            status = string.IsNullOrWhiteSpace(jobStatus) ? "queued" : jobStatus,
            jobId,
            symbol,
            timeHorizon
        });
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

