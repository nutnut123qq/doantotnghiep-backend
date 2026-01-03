using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Messaging;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly INewsService _newsService;
    private readonly RabbitMQService? _rabbitMQService;
    private readonly ILogger<NewsController> _logger;

    public NewsController(
        INewsService newsService,
        ILogger<NewsController> logger,
        RabbitMQService? rabbitMQService = null)
    {
        _newsService = newsService;
        _rabbitMQService = rabbitMQService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? tickerId = null)
    {
        try
        {
            var news = await _newsService.GetNewsAsync(page, pageSize, tickerId);
            // Return empty list instead of null
            return Ok(news ?? Enumerable.Empty<object>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news");
            // Return empty array instead of 500
            return Ok(new List<object>());
        }
    }

    [HttpPost("{id}/summarize")]
    public Task<IActionResult> RequestSummarization(Guid id)
    {
        try
        {
            // Publish message to queue for async processing
            if (_rabbitMQService != null)
            {
                _rabbitMQService.Publish("news_summarize", new { NewsId = id });
                return Task.FromResult<IActionResult>(Accepted(new { message = "Summarization request queued", newsId = id }));
            }
            else
            {
                return Task.FromResult<IActionResult>(Accepted(new { message = "Message queue unavailable, summarization disabled", newsId = id }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing summarization request for news {Id}", id);
            return Task.FromResult<IActionResult>(StatusCode(500, "An error occurred while queuing summarization"));
        }
    }
}

