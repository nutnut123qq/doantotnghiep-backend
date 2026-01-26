using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Messaging;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly INewsService _newsService;
    private readonly IAIService? _aiService;
    private readonly RabbitMQService? _rabbitMQService;
    private readonly ILogger<NewsController> _logger;

    public NewsController(
        INewsService newsService,
        ILogger<NewsController> logger,
        IAIService? aiService = null,
        RabbitMQService? rabbitMQService = null)
    {
        _newsService = newsService;
        _aiService = aiService;
        _rabbitMQService = rabbitMQService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? tickerId = null)
    {
        var news = await _newsService.GetNewsAsync(page, pageSize, tickerId);
        // Return empty list instead of null if no news found
        return Ok(news ?? Enumerable.Empty<object>());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNewsById(Guid id)
    {
        var news = await _newsService.GetNewsByIdAsync(id);
        if (news == null)
            return NotFound($"News with ID {id} not found");
            
        return Ok(news);
    }

    /// <summary>
    /// Request summarization for a news article
    /// P0-1: Requires authentication to prevent abuse and protect AI service costs
    /// </summary>
    [HttpPost("{id}/summarize")]
    [Authorize] // P0-1: Require authentication (all authenticated users can use, but rate limiting should be applied)
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RequestSummarization(Guid id)
    {
        // Nếu có RabbitMQ, queue như cũ
        if (_rabbitMQService != null)
        {
            _rabbitMQService.Publish("news_summarize", new { NewsId = id });
            return Accepted(new { message = "Summarization request queued", newsId = id });
        }
        
        // Fallback sync nếu không có RabbitMQ
        if (_aiService == null)
        {
            return Problem(
                detail: "AI service is not available",
                statusCode: 503);
        }

        var news = await _newsService.GetNewsByIdAsync(id);
        if (news == null)
            return NotFound($"News with ID {id} not found");
            
        // Call AI service
        var summaryResult = await _aiService.SummarizeNewsDetailedAsync(news.Content);
        
        // Update DB
        news.Summary = summaryResult.Summary;
        news.Sentiment = ParseSentiment(summaryResult.Sentiment);
        news.ImpactAssessment = summaryResult.ImpactAssessment;
        await _newsService.UpdateNewsAsync(news);
        
        return Ok(summaryResult);
    }

    private Sentiment? ParseSentiment(string sentimentString)
    {
        if (string.IsNullOrWhiteSpace(sentimentString))
            return null;

        return sentimentString.ToLower() switch
        {
            "positive" => Sentiment.Positive,
            "negative" => Sentiment.Negative,
            "neutral" => Sentiment.Neutral,
            _ => Sentiment.Neutral
        };
    }
}

