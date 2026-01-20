using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Api.Controllers;

/// <summary>
/// Controller for Analysis Reports module
/// P0 Fix #7: [Authorize] for security, plural naming for REST convention
/// </summary>
[ApiController]
[Route("api/analysis-reports")] // ✅ Plural for REST convention (P0 Fix #9)
[Authorize] // ✅ Require authentication (P0 Fix #7)
public class AnalysisReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAnalysisReportQAService _qaService;
    private readonly IAIService _aiService;
    private readonly ILogger<AnalysisReportsController> _logger;

    public AnalysisReportsController(
        ApplicationDbContext context,
        IAnalysisReportQAService qaService,
        IAIService aiService,
        ILogger<AnalysisReportsController> logger)
    {
        _context = context;
        _qaService = qaService;
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Get list of analysis reports by symbol (paginated)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string symbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest("Symbol is required");

        // ✅ P0 Fix #12: CRITICAL - Normalize symbol
        symbol = symbol.ToUpperInvariant().Trim();

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _context.AnalysisReports
            .AsNoTracking() // ✅ Better performance for read-only queries
            .Where(r => r.Symbol == symbol) // Already normalized in DB
            .OrderByDescending(r => r.PublishedAt);

        var total = await query.CountAsync();
        var reports = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = reports.Select(r => new AnalysisReportListDto
        {
            Id = r.Id,
            Symbol = r.Symbol,
            Title = r.Title,
            FirmName = r.FirmName,
            PublishedAt = r.PublishedAt,
            Recommendation = r.Recommendation,
            TargetPrice = r.TargetPrice,
            SourceUrl = r.SourceUrl,
            ContentPreview = Cap(r.Content, 200) // ✅ SAFE (P0 Fix #3)
        }).ToList();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>
    /// Get analysis report detail by ID (with full content)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var report = await _context.AnalysisReports.FindAsync(id);

        if (report == null)
            return NotFound($"Analysis report with ID {id} not found");

        var detailDto = new AnalysisReportDetailDto
        {
            Id = report.Id,
            Symbol = report.Symbol,
            Title = report.Title,
            FirmName = report.FirmName,
            PublishedAt = report.PublishedAt,
            Recommendation = report.Recommendation,
            TargetPrice = report.TargetPrice,
            Content = report.Content, // ✅ Full content
            SourceUrl = report.SourceUrl,
            CreatedAt = report.CreatedAt
        };

        return Ok(detailDto);
    }

    /// <summary>
    /// Create a new analysis report (V1: plain text only, NO crawler)
    /// </summary>
    [HttpPost]
    // Optional: [Authorize(Roles = "Admin,Analyst")] // Restrict to specific roles
    public async Task<IActionResult> Create([FromBody] CreateAnalysisReportDto dto)
    {
        // ✅ P0 Fix #11: CRITICAL - Validate input
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest("Content is required");

        var report = new AnalysisReport
        {
            Symbol = dto.Symbol.ToUpperInvariant().Trim(), // ✅ P0 Fix #12: Normalize symbol
            Title = dto.Title.Trim(), // ✅ Trim whitespace
            FirmName = dto.FirmName.Trim(),
            PublishedAt = dto.PublishedAt,
            Recommendation = dto.Recommendation?.Trim(),
            TargetPrice = dto.TargetPrice,
            Content = dto.Content, // V1: plain text only
            SourceUrl = string.IsNullOrWhiteSpace(dto.SourceUrl)
                ? null
                : dto.SourceUrl.Trim() // ✅ P0 Fix #6: Optional, no crawler fallback
        };

        _context.AnalysisReports.Add(report);
        await _context.SaveChangesAsync();

        // RAG ingestion (do not fail report creation on ingest error)
        try
        {
            var ingestResult = await _aiService.IngestDocumentAsync(
                documentId: report.Id.ToString(),
                source: "analysis_report",
                text: report.Content,
                metadata: new
                {
                    symbol = report.Symbol,
                    title = report.Title,
                    sourceUrl = report.SourceUrl,
                    firmName = report.FirmName,
                    publishedAt = report.PublishedAt
                },
                cancellationToken: default);
            
            _logger.LogInformation(
                "Successfully ingested report {ReportId}: {ChunksUpserted} chunks to {Collection}",
                report.Id, ingestResult.ChunksUpserted, ingestResult.Collection);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RAG ingest failed for report {ReportId}, but report creation succeeded",
                report.Id);
        }

        _logger.LogInformation("Created analysis report {ReportId} for {Symbol}",
            report.Id, report.Symbol);

        var detailDto = new AnalysisReportDetailDto
        {
            Id = report.Id,
            Symbol = report.Symbol,
            Title = report.Title,
            FirmName = report.FirmName,
            PublishedAt = report.PublishedAt,
            Recommendation = report.Recommendation,
            TargetPrice = report.TargetPrice,
            Content = report.Content,
            SourceUrl = report.SourceUrl,
            CreatedAt = report.CreatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = report.Id }, detailDto);
    }

    /// <summary>
    /// Ask a question about an analysis report (Q&A with citations)
    /// </summary>
    [HttpPost("{id}/qa")]
    public async Task<IActionResult> AskQuestion(Guid id, [FromBody] AskQuestionDto dto)
    {
        // ✅ P0 Fix #11: CRITICAL - Validate input
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _qaService.AskQuestionAsync(id, dto.Question);

            return Ok(new QAResponseDto
            {
                Answer = result.Answer,
                Citations = result.Citations.Select(c => new CitationDto
                {
                    CitationNumber = c.CitationNumber, // ✅ P0 Fix #8: Keep original index
                    SourceType = c.SourceType,
                    SourceId = c.SourceId,
                    Title = c.Title,
                    Url = c.Url,
                    Excerpt = c.Excerpt
                }).ToList()
            });
        }
        catch (Domain.Exceptions.NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering question for report {ReportId}", id);
            return StatusCode(500, "An error occurred while processing your question. Please try again later.");
        }
    }

    /// <summary>
    /// Helper method for safe string capping
    /// P0 Fix #2, #3: Accept nullable string and safely cap to maxLength
    /// </summary>
    private static string Cap(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "…";
    }
}
