using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Messaging;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly INewsService _newsService;
    private readonly RabbitMQService _rabbitMQService;
    private readonly ILogger<NewsController> _logger;

    public NewsController(
        INewsService newsService,
        RabbitMQService rabbitMQService,
        ILogger<NewsController> logger)
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
            return Ok(news);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news");
            return StatusCode(500, "An error occurred while fetching news");
        }
    }

    [HttpPost("{id}/summarize")]
    public Task<IActionResult> RequestSummarization(Guid id)
    {
        try
        {
            // Publish message to queue for async processing
            _rabbitMQService.Publish("news_summarize", new { NewsId = id });

            return Task.FromResult<IActionResult>(Accepted(new { message = "Summarization request queued", newsId = id }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing summarization request for news {Id}", id);
            return Task.FromResult<IActionResult>(StatusCode(500, "An error occurred while queuing summarization"));
        }
    }
}

