using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;
using StockInvestment.Infrastructure.Data;
using System.Text;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Service for Q&A functionality on Analysis Reports
/// V1 Minimal: NO RAG, builds context from DB directly
/// </summary>
public class AnalysisReportQAService : IAnalysisReportQAService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly IFinancialReportService _financialReportService;
    private readonly INewsService _newsService;
    private readonly ILogger<AnalysisReportQAService> _logger;

    // Context limits
    private const int MAX_REPORT_CONTENT_LENGTH = 12000;
    private const int MAX_FINANCIAL_CONTEXT_LENGTH = 3000;
    private const int MAX_NEWS_CONTEXT_LENGTH = 4000;
    private const int MAX_NEWS_ITEM_SNIPPET_LENGTH = 600;
    private const int NEWS_LIMIT = 5;
    private const int NEWS_WINDOW_DAYS = 7;

    public AnalysisReportQAService(
        ApplicationDbContext context,
        IAIService aiService,
        IFinancialReportService financialReportService,
        INewsService newsService,
        ILogger<AnalysisReportQAService> logger)
    {
        _context = context;
        _aiService = aiService;
        _financialReportService = financialReportService;
        _newsService = newsService;
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

        // 2. Build cross-reference context
        var reportContext = BuildReportContext(report);
        var financialSnapshot = await TryGetFinancialSnapshotAsync(report.Symbol, cancellationToken);
        var financialContext = BuildFinancialContext(financialSnapshot);
        var recentNews = await TryGetNewsItemsAsync(report.Symbol, cancellationToken);
        var newsContext = BuildNewsContext(recentNews);

        var contextParts = new List<string>
        {
            reportContext,
            financialContext,
            newsContext
        };

        var baseContext = string.Join("\n\n---\n\n", contextParts);
        _logger.LogDebug("Base context prepared for RAG Q&A (length={Length})", baseContext.Length);

        // 3. Ingest supplemental contexts (finance + news) for better citations
        await TryIngestSupplementalContextsAsync(
            report.Symbol,
            financialSnapshot,
            recentNews,
            financialContext,
            cancellationToken);

        // 4. Call RAG-enabled Q&A with symbol-only filter (multi-source)
        var result = await _aiService.AnswerQuestionAsync(
            question: question,
            baseContext: baseContext,
            documentId: null,
            source: null,
            symbol: report.Symbol,
            topK: 8,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "RAG Q&A returned {SourceCount} sources for report {ReportId}",
            result.Sources.Count, reportId);

        // 5. Map source objects to CitationDto
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

    private string BuildReportContext(StockInvestment.Domain.Entities.AnalysisReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("REPORT CONTEXT");
        builder.AppendLine($"Title: {report.Title}");
        builder.AppendLine($"Firm: {report.FirmName}");
        builder.AppendLine($"Published: {report.PublishedAt:yyyy-MM-dd}");
        builder.AppendLine($"Recommendation: {report.Recommendation ?? "N/A"}");
        builder.AppendLine($"Target Price: {(report.TargetPrice?.ToString("N0") ?? "N/A")} VND");
        builder.AppendLine($"Source: {report.SourceUrl ?? "N/A"}");
        builder.AppendLine();
        builder.AppendLine("CONTENT:");
        builder.AppendLine(Cap(report.Content, MAX_REPORT_CONTENT_LENGTH));

        return builder.ToString().Trim();
    }

    private string BuildFinancialContext(FinancialSnapshotDto? snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("FINANCIAL CONTEXT");
        builder.AppendLine("FINANCIAL SNAPSHOT (latest):");

        if (snapshot == null)
        {
            builder.AppendLine("No financial snapshot available.");
            return builder.ToString().Trim();
        }

        builder.AppendLine($"- Period: {snapshot.Period ?? "N/A"}");
        builder.AppendLine($"- Revenue: {FormatDecimal(snapshot.Revenue)}");
        builder.AppendLine($"- Net profit: {FormatDecimal(snapshot.NetProfit)}");
        builder.AppendLine($"- EPS: {FormatDecimal(snapshot.Eps)}");
        builder.AppendLine($"- P/E: {FormatDecimal(snapshot.Pe)}");
        builder.AppendLine($"- ROE: {FormatDecimal(snapshot.Roe)}");
        builder.AppendLine($"- Debt/Equity: {FormatDecimal(snapshot.DebtToEquity)}");

        var notes = snapshot.Notes ?? snapshot.RawText ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.AppendLine($"- Notes: {Cap(notes, 1200)}");
        }

        return Cap(builder.ToString().Trim(), MAX_FINANCIAL_CONTEXT_LENGTH);
    }

    private string BuildNewsContext(IReadOnlyList<NewsItemDto> newsItems)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"RELATED NEWS (last {NEWS_WINDOW_DAYS} days):");

        if (newsItems.Count == 0)
        {
            builder.AppendLine("No recent news found.");
            return builder.ToString().Trim();
        }

        var index = 1;
        foreach (var item in newsItems.Take(NEWS_LIMIT))
        {
            builder.AppendLine($"[{index}] {item.PublishedAt:yyyy-MM-dd} - {item.Title} ({item.Url ?? "N/A"})");

            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                builder.AppendLine(Cap(item.Summary, MAX_NEWS_ITEM_SNIPPET_LENGTH));
            }

            builder.AppendLine();
            index++;
        }

        return Cap(builder.ToString().Trim(), MAX_NEWS_CONTEXT_LENGTH);
    }

    private async Task<FinancialSnapshotDto?> TryGetFinancialSnapshotAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            return await _financialReportService.GetLatestFinancialSnapshotAsync(symbol);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load financial snapshot for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<IReadOnlyList<NewsItemDto>> TryGetNewsItemsAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            return await _newsService.GetRecentNewsForSymbolAsync(symbol, NEWS_WINDOW_DAYS, NEWS_LIMIT);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load recent news for {Symbol}", symbol);
            return Array.Empty<NewsItemDto>();
        }
    }

    private async Task TryIngestSupplementalContextsAsync(
        string symbol,
        FinancialSnapshotDto? snapshot,
        IReadOnlyList<NewsItemDto> newsItems,
        string financialContextText,
        CancellationToken cancellationToken)
    {
        try
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(financialContextText))
            {
                var reportDate = snapshot.ReportDate ?? DateTime.UtcNow;
                var documentId = $"finance:{symbol}:{reportDate:yyyyMMdd}";
                await _aiService.IngestDocumentAsync(
                    documentId: documentId,
                    source: "financial_report",
                    text: financialContextText,
                    metadata: new
                    {
                        title = $"Financial Snapshot {symbol}",
                        symbol,
                        section = "financial",
                        sourceUrl = snapshot.SourceUrl
                    },
                    cancellationToken: cancellationToken);
            }

            foreach (var item in newsItems)
            {
                var newsText = BuildNewsItemText(item);
                if (string.IsNullOrWhiteSpace(newsText))
                {
                    continue;
                }

                var documentId = $"news:{symbol}:{item.Id}";
                await _aiService.IngestDocumentAsync(
                    documentId: documentId,
                    source: "news",
                    text: newsText,
                    metadata: new
                    {
                        title = item.Title,
                        symbol,
                        section = "news",
                        sourceUrl = item.Url
                    },
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ingest supplemental contexts for {Symbol}", symbol);
        }
    }

    private static string BuildNewsItemText(NewsItemDto item)
    {
        var builder = new StringBuilder();
        builder.AppendLine(item.Title);
        builder.AppendLine($"Published: {item.PublishedAt:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(item.Url))
        {
            builder.AppendLine($"Url: {item.Url}");
        }

        if (!string.IsNullOrWhiteSpace(item.Summary))
        {
            builder.AppendLine();
            builder.AppendLine(item.Summary);
        }

        return builder.ToString().Trim();
    }

    private static string FormatDecimal(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("N2") : "N/A";
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
