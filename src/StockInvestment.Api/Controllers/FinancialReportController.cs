using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Api.Contracts.Responses;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinancialReportController : ControllerBase
{
    private readonly IFinancialReportService _reportService;
    private readonly IFinancialReportCrawlerService _crawlerService;
    private readonly ILogger<FinancialReportController> _logger;

    public FinancialReportController(
        IFinancialReportService reportService,
        IFinancialReportCrawlerService crawlerService,
        ILogger<FinancialReportController> logger)
    {
        _reportService = reportService;
        _crawlerService = crawlerService;
        _logger = logger;
    }

    /// <summary>
    /// Get financial reports by ticker ID
    /// </summary>
    [HttpGet("ticker/{tickerId}")]
    public async Task<IActionResult> GetByTicker(Guid tickerId)
    {
        try
        {
            var reports = await _reportService.GetReportsByTickerAsync(tickerId);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reports for ticker {TickerId}", tickerId);
            return StatusCode(500, "Error fetching financial reports");
        }
    }

    /// <summary>
    /// Get financial reports by symbol
    /// </summary>
    [HttpGet("symbol/{symbol}")]
    public async Task<IActionResult> GetBySymbol(string symbol)
    {
        try
        {
            var reports = await _reportService.GetReportsBySymbolAsync(symbol);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reports for symbol {Symbol}", symbol);
            return StatusCode(500, "Error fetching financial reports");
        }
    }

    /// <summary>
    /// Get financial report by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var report = await _reportService.GetReportByIdAsync(id);
            if (report == null)
            {
                return NotFound();
            }
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting report {Id}", id);
            return StatusCode(500, "Error fetching financial report");
        }
    }

    /// <summary>
    /// Crawl and save financial reports for a symbol
    /// </summary>
    [HttpPost("crawl/{symbol}")]
    public async Task<IActionResult> CrawlReports(string symbol, [FromQuery] int maxReports = 10)
    {
        try
        {
            var reports = await _crawlerService.CrawlReportsBySymbolAsync(symbol, maxReports);
            var reportsList = reports.ToList();

            if (reportsList.Any())
            {
                // Note: We need to set TickerId before saving
                // This would require looking up the ticker first
                _logger.LogInformation("Crawled {Count} reports for {Symbol}", reportsList.Count, symbol);
            }

            return Ok(new { symbol, count = reportsList.Count, reports = reportsList });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling reports for {Symbol}", symbol);
            return StatusCode(500, "Error crawling financial reports");
        }
    }

    /// <summary>
    /// Ask a question about a financial report using AI
    /// </summary>
    [HttpPost("{id}/ask")]
    [ProducesResponseType(typeof(AskQuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AskQuestion(Guid id, [FromBody] AskQuestionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest("Question is required");
            }

            var result = await _reportService.AskQuestionAsync(id, request.Question);
            
            var response = new AskQuestionResponse
            {
                Question = request.Question,
                Answer = result.Answer,
                Sources = result.Sources
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering question for report {Id}", id);
            return StatusCode(500, "Error processing question");
        }
    }
}

public class AskQuestionRequest
{
    public string Question { get; set; } = string.Empty;
}

