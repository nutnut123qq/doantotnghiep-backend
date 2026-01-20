using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Service for Q&A functionality on Analysis Reports
/// V1 Minimal: NO RAG, builds context from DB directly
/// </summary>
public class AnalysisReportQAService : IAnalysisReportQAService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ILogger<AnalysisReportQAService> _logger;

    // Context limits (P1 improvement - using constants)
    private const int FINANCIAL_REPORTS_LIMIT = 2;
    private const int NEWS_LIMIT = 5;

    public AnalysisReportQAService(
        ApplicationDbContext context,
        IAIService aiService,
        ILogger<AnalysisReportQAService> logger)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<QAResult> AskQuestionAsync(
        Guid reportId,
        string question,
        CancellationToken cancellationToken = default)
    {
        // 1. Load report from DB
        var report = await _context.AnalysisReports.FindAsync(new object[] { reportId }, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException($"Analysis report with ID {reportId} not found");
        }

        _logger.LogInformation("Using RAG Q&A for report {ReportId} ({Symbol})", reportId, report.Symbol);

        // 2. Build short base context (metadata only, not full content)
        var baseContext = $@"Report: {report.Title}
Firm: {report.FirmName}
Recommendation: {report.Recommendation ?? "N/A"}
Target Price: {report.TargetPrice?.ToString("N0") ?? "N/A"} VND
Published: {report.PublishedAt:yyyy-MM-dd}";

        _logger.LogDebug("Base context prepared for RAG Q&A");

        // 3. Call RAG-enabled Q&A with filters to retrieve from THIS report only
        var result = await _aiService.AnswerQuestionAsync(
            question: question,
            baseContext: baseContext,
            documentId: report.Id.ToString(), // ✅ CRITICAL: Filter by reportId
            source: "analysis_report",
            symbol: report.Symbol,
            topK: 6,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "RAG Q&A returned {SourceCount} sources for report {ReportId}",
            result.Sources.Count, reportId);

        // 4. Map source objects to CitationDto
        var citations = result.Sources
            .Select((src, idx) => new CitationDto
            {
                CitationNumber = idx + 1, // Sequential citation numbers
                SourceId = src.DocumentId, // reportId from RAG
                SourceType = src.Source,
                Title = src.Title,
                Url = src.SourceUrl,
                Excerpt = src.TextPreview // Already capped by AI service
            })
            .ToList();

        _logger.LogInformation(
            "Generated answer with {CitationCount} citations from RAG for report {ReportId}",
            citations.Count, reportId);

        return new QAResult
        {
            Answer = result.Answer, // From RAG Q&A
            Citations = citations
        };
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
