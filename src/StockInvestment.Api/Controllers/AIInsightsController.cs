using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.DTOs.AIInsights;
using StockInvestment.Domain.Enums;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AIInsightsController : ControllerBase
{
    private readonly IAIInsightService _insightService;
    private readonly ILogger<AIInsightsController> _logger;
    private readonly ICacheService _cacheService;
    private readonly ApplicationDbContext _context;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;

    public AIInsightsController(
        IAIInsightService insightService,
        ILogger<AIInsightsController> logger,
        ICacheService cacheService,
        ApplicationDbContext context,
        ICacheKeyGenerator cacheKeyGenerator)
    {
        _insightService = insightService;
        _logger = logger;
        _cacheService = cacheService;
        _context = context;
        _cacheKeyGenerator = cacheKeyGenerator;
    }

    /// <summary>
    /// Get all AI insights with optional filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInsights(
        [FromQuery] string? type = null,
        [FromQuery] string? symbol = null,
        [FromQuery] bool includeDismissed = false)
    {
        // Check cache
        var cacheKey = _cacheKeyGenerator.GenerateInsightsKey(type, symbol, includeDismissed);
        var cachedInsights = await _cacheService.GetAsync<List<object>>(cacheKey);
        if (cachedInsights != null)
        {
            return Ok(cachedInsights);
        }

        InsightType? insightType = null;
        if (!string.IsNullOrEmpty(type))
        {
            if (Enum.TryParse<InsightType>(type, true, out var parsedType))
            {
                insightType = parsedType;
            }
        }

        var insights = await _insightService.GetInsightsAsync(insightType, symbol, includeDismissed);

        // Map to DTO
        var result = insights.Select(MapToDto).ToList();

        // Cache for 15 minutes
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        return Ok(result);
    }

    /// <summary>
    /// Map AIInsight entity to DTO
    /// </summary>
    private static object MapToDto(Domain.Entities.AIInsight i)
    {
        return new
        {
            id = i.Id,
            symbol = i.Ticker.Symbol,
            name = i.Ticker.Name,
            type = i.Type.ToString(),
            title = i.Title,
            description = i.Description,
            confidence = i.Confidence,
            reasoning = JsonSerializer.Deserialize<List<string>>(i.Reasoning) ?? new List<string>(),
            targetPrice = i.TargetPrice,
            stopLoss = i.StopLoss,
            timestamp = i.GeneratedAt,
            generatedAt = i.GeneratedAt
        };
    }

    /// <summary>
    /// Get insight details by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetInsightById(Guid id)
    {
        var insight = await _insightService.GetInsightByIdAsync(id);
        if (insight == null)
        {
            return NotFound();
        }

        var result = MapToDto(insight);
        return Ok(result);
    }

    /// <summary>
    /// Get market sentiment analysis
    /// </summary>
    [HttpGet("sentiment")]
    public async Task<IActionResult> GetMarketSentiment()
    {
        // Check cache
        var cacheKey = _cacheKeyGenerator.GenerateMarketSentimentKey();
        var cachedSentiment = await _cacheService.GetAsync<MarketSentiment>(cacheKey);
        if (cachedSentiment != null)
        {
            return Ok(cachedSentiment);
        }

        var sentiment = await _insightService.GetMarketSentimentAsync();

        // Cache for 15 minutes
        await _cacheService.SetAsync(cacheKey, sentiment, TimeSpan.FromMinutes(15));

        return Ok(sentiment);
    }

    /// <summary>
    /// Dismiss an insight (mark as read/irrelevant)
    /// </summary>
    [HttpPost("{id}/dismiss")]
    public async Task<IActionResult> DismissInsight(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        await _insightService.DismissInsightAsync(id, userId);

        // Invalidate cache
        await _cacheService.RemoveByPatternAsync(_cacheKeyGenerator.GeneratePattern("insights"));
        await _cacheService.RemoveAsync(_cacheKeyGenerator.GenerateMarketSentimentKey());

            return Ok(new DismissInsightResponseDto());
    }

    /// <summary>
    /// Manually trigger insight generation for a ticker
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateInsight([FromBody] GenerateInsightRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest(new { error = "Symbol is required" });
            }

            // P2-1: Use repository instead of DbContext
            var ticker = await _tickerRepository.GetBySymbolAsync(request.Symbol.ToUpper());

            if (ticker == null)
            {
                return NotFound(new { error = $"Ticker {request.Symbol} not found" });
            }

            _logger.LogInformation("Generating insight for {Symbol} (TickerId: {TickerId})", request.Symbol, ticker.Id);

            var insight = await _insightService.GenerateInsightAsync(ticker.Id);

            // Invalidate cache
            await _cacheService.RemoveByPatternAsync(_cacheKeyGenerator.GeneratePattern("insights"));
            await _cacheService.RemoveAsync(_cacheKeyGenerator.GenerateMarketSentimentKey());

            var result = new
            {
                id = insight.Id,
                symbol = insight.Ticker.Symbol,
                name = insight.Ticker.Name,
                type = insight.Type.ToString(),
                title = insight.Title,
                description = insight.Description,
                confidence = insight.Confidence,
                reasoning = JsonSerializer.Deserialize<List<string>>(insight.Reasoning) ?? new List<string>(),
                targetPrice = insight.TargetPrice,
                stopLoss = insight.StopLoss,
                generatedAt = insight.GeneratedAt
            };

            return Ok(result);
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error generating insight for {Symbol}", request.Symbol);
            
            // Check if it's a connection error
            if (httpEx.Message.Contains("Connection refused") || httpEx.Message.Contains("No connection could be made"))
            {
                return StatusCode(503, new 
                { 
                    error = "AI service unavailable",
                    message = "Không thể kết nối đến AI service. Vui lòng kiểm tra AI service đang chạy tại http://localhost:8000",
                    symbol = request.Symbol
                });
            }
            
            return StatusCode(500, new 
            { 
                error = "AI service error",
                message = $"Lỗi khi gọi AI service: {httpEx.Message}",
                symbol = request.Symbol
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating insight for {Symbol}. StackTrace: {StackTrace}", request.Symbol, ex.StackTrace);
            
            // Provide more detailed error information
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" | Inner: {ex.InnerException.Message}";
            }
            
            return StatusCode(500, new 
            { 
                error = "Failed to generate insight",
                message = errorMessage,
                symbol = request.Symbol,
                details = "Có thể do: AI service không chạy, lỗi kết nối, hoặc lỗi xử lý dữ liệu"
            });
        }
    }
}
