using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Api.Contracts.Responses;
using StockInvestment.Domain.Exceptions;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinancialReportController : ControllerBase
{
    private readonly IFinancialReportService _reportService;
    private readonly ApplicationDbContext _context;

    public FinancialReportController(
        IFinancialReportService reportService,
        ApplicationDbContext context)
    {
        _reportService = reportService;
        _context = context;
    }

    /// <summary>
    /// Get financial reports by ticker ID
    /// </summary>
    [HttpGet("ticker/{tickerId}")]
    public async Task<IActionResult> GetByTicker(Guid tickerId)
    {
        var reports = await _reportService.GetReportsByTickerAsync(tickerId);
        return Ok(reports);
    }

    /// <summary>
    /// Get financial reports by symbol
    /// </summary>
    [HttpGet("symbol/{symbol}")]
    public async Task<IActionResult> GetBySymbol(string symbol)
    {
        var reports = await _reportService.GetReportsBySymbolAsync(symbol);
        return Ok(reports);
    }

    /// <summary>
    /// Get financial report by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var report = await _reportService.GetReportByIdAsync(id);
        if (report == null)
        {
            return NotFound();
        }
        return Ok(report);
    }

    /// <summary>
    /// Crawl and save financial reports for a symbol
    /// </summary>
    [HttpPost("crawl/{symbol}")]
    public async Task<IActionResult> CrawlReports(string symbol, [FromQuery] int maxReports = 10)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        var ticker = await _context.StockTickers.FirstOrDefaultAsync(t => t.Symbol == normalizedSymbol);
        if (ticker == null)
        {
            return NotFound($"Ticker {normalizedSymbol} not found");
        }

        var reportsList = (await _reportService.CrawlAndPersistReportsForSymbolAsync(normalizedSymbol, maxReports)).ToList();
        return Ok(new { symbol, count = reportsList.Count, reports = reportsList });
    }

    /// <summary>
    /// Ask a question about a financial report using AI
    /// </summary>
    [HttpPost("{id}/ask")]
    [ProducesResponseType(typeof(AskQuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AskQuestion(Guid id, [FromBody] AskQuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required");
        }

        try
        {
            var result = await _reportService.AskQuestionAsync(id, request.Question);
            var sources = result.Sources
                .Select(s => new QASourceResponse
                {
                    Title = string.IsNullOrWhiteSpace(s.Title) ? "Financial report source" : s.Title,
                    Url = s.SourceUrl,
                    SourceType = string.IsNullOrWhiteSpace(s.Source) ? "financial_report" : s.Source,
                    PublishedAt = null
                })
                .ToList();

            var response = new AskQuestionResponse
            {
                Question = request.Question,
                Answer = result.Answer,
                Sources = sources
            };

            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            // PostAsJsonAsync can throw for connection failures; HandleHttpErrorAsync throws when Python returns an HTTP error body.
            var fromAiHttp = ex.Message.StartsWith("AI service returned", StringComparison.Ordinal);
            var message = fromAiHttp
                ? "Dịch vụ AI (Python) đã nhận yêu cầu nhưng xử lý thất bại (thường do LLM: API key, hạn mức, hoặc tài khoản Blackbox/email bị chặn). Xem trường detail."
                : "Không kết nối được dịch vụ AI (Python). Hãy chạy service AI (ví dụ uvicorn), kiểm tra AIService:BaseUrl và mạng/firewall.";
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message,
                detail = ex.Message
            });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "Dịch vụ AI phản hồi quá lâu hoặc bị hủy. Vui lòng thử lại."
            });
        }
        catch (ExternalServiceException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Dịch vụ AI trả về lỗi hoặc dữ liệu không hợp lệ.",
                detail = ex.Message
            });
        }
    }
}

public class AskQuestionRequest
{
    public string Question { get; set; } = string.Empty;
}

