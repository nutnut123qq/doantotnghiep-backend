using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.DTOs.Common;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;
using System.Text;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Services;

public class FinancialReportService : IFinancialReportService
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIService _aiService;
    private readonly IFinancialReportCrawlerService _crawlerService;
    private readonly ILogger<FinancialReportService> _logger;

    public FinancialReportService(
        ApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IAIService aiService,
        IFinancialReportCrawlerService crawlerService,
        ILogger<FinancialReportService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _crawlerService = crawlerService;
        _logger = logger;
    }

    public async Task<IEnumerable<FinancialReport>> GetReportsByTickerAsync(Guid tickerId)
    {
        return await _context.FinancialReports
            .Where(r => r.TickerId == tickerId && !r.IsDeleted)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Quarter)
            .ToListAsync();
    }

    public async Task<IEnumerable<FinancialReport>> GetReportsBySymbolAsync(string symbol)
    {
        var ticker = await _context.StockTickers
            .FirstOrDefaultAsync(t => t.Symbol == symbol.ToUpper());

        if (ticker == null)
        {
            throw new Domain.Exceptions.NotFoundException("StockTicker", symbol);
        }

        return await GetReportsByTickerAsync(ticker.Id);
    }

    public async Task<(IReadOnlyList<FinancialReport> Items, int TotalCount)> GetReportsForAdminAsync(int page = 1, int pageSize = 20, string? symbol = null)
    {
        var query = _context.FinancialReports
            .Include(r => r.Ticker)
            .AsQueryable();

        var normalizedSymbol = symbol?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            query = query.Where(r => r.Ticker.Symbol == normalizedSymbol);
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.ReportDate)
            .ThenByDescending(r => r.Year)
            .ThenByDescending(r => r.Quarter ?? 0)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public async Task<FinancialSnapshotDto?> GetLatestFinancialSnapshotAsync(string symbol)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        var ticker = await _context.StockTickers
            .FirstOrDefaultAsync(t => t.Symbol == normalizedSymbol);

        if (ticker == null)
        {
            _logger.LogWarning("No ticker found for symbol {Symbol}", normalizedSymbol);
            return null;
        }

        var latestReport = await _context.FinancialReports
            .Where(r => r.TickerId == ticker.Id && !r.IsDeleted)
            .OrderByDescending(r => r.ReportDate)
            .ThenByDescending(r => r.Year)
            .ThenByDescending(r => r.Quarter ?? 0)
            .FirstOrDefaultAsync();

        if (latestReport == null)
        {
            _logger.LogInformation("No financial report found for symbol {Symbol}", normalizedSymbol);
            return null;
        }

        var snapshot = new FinancialSnapshotDto
        {
            Symbol = normalizedSymbol,
            Period = BuildPeriodLabel(latestReport),
            ReportDate = latestReport.ReportDate,
            SourceUrl = "internal://financial-report",
            RawText = latestReport.Content
        };

        if (TryParseSnapshot(latestReport.Content, snapshot))
        {
            return snapshot;
        }

        snapshot.Notes = Cap(latestReport.Content, 1200);
        return snapshot;
    }

    public async Task<FinancialReport?> GetReportByIdAsync(Guid id)
    {
        var report = await _unitOfWork.Repository<FinancialReport>().GetByIdAsync(id);
        if (report == null || report.IsDeleted)
        {
            return null;
        }

        return report;
    }

    public async Task<bool> SetReportDeletedAsync(Guid id, bool isDeleted)
    {
        var report = await _unitOfWork.Repository<FinancialReport>().GetByIdAsync(id);
        if (report == null)
        {
            return false;
        }

        if (report.IsDeleted == isDeleted)
        {
            return true;
        }

        report.IsDeleted = isDeleted;
        await _unitOfWork.Repository<FinancialReport>().UpdateAsync(report);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<FinancialReport> AddReportAsync(FinancialReport report)
    {
        try
        {
            await _unitOfWork.Repository<FinancialReport>().AddAsync(report);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Added financial report {Id} for ticker {TickerId}", report.Id, report.TickerId);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding financial report");
            throw;
        }
    }

    public async Task<IEnumerable<FinancialReport>> AddReportsRangeAsync(IEnumerable<FinancialReport> reports)
    {
        try
        {
            var reportsList = reports.ToList();
            await _unitOfWork.Repository<FinancialReport>().AddRangeAsync(reportsList);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Added {Count} financial reports", reportsList.Count);
            return reportsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding financial reports");
            throw;
        }
    }

    public async Task<IReadOnlyList<FinancialReport>> CrawlAndPersistReportsForSymbolAsync(string symbol, int maxReports, CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        var ticker = await _context.StockTickers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Symbol == normalizedSymbol, cancellationToken);

        if (ticker == null)
        {
            _logger.LogWarning("Crawl skipped: ticker {Symbol} not found", normalizedSymbol);
            return Array.Empty<FinancialReport>();
        }

        var crawled = (await _crawlerService.CrawlReportsBySymbolAsync(normalizedSymbol, maxReports)).ToList();
        foreach (var report in crawled)
        {
            report.TickerId = ticker.Id;
        }

        var existingKeys = await _context.FinancialReports
            .Where(r => r.TickerId == ticker.Id)
            .Select(r => new { r.ReportType, r.Year, r.Quarter, r.ReportDate })
            .ToListAsync(cancellationToken);

        var toInsert = crawled
            .Where(r => !existingKeys.Any(e =>
                e.ReportType == r.ReportType
                && e.Year == r.Year
                && e.Quarter == r.Quarter
                && e.ReportDate == r.ReportDate))
            .ToList();

        if (toInsert.Count == 0)
        {
            return Array.Empty<FinancialReport>();
        }

        await AddReportsRangeAsync(toInsert);
        return toInsert;
    }

    public async Task<QuestionAnswerResult> AskQuestionAsync(Guid reportId, string question)
    {
        var report = await GetReportByIdAsync(reportId);
        if (report == null)
        {
            throw new Domain.Exceptions.NotFoundException("FinancialReport", reportId);
        }

        var symbol = await _context.StockTickers
            .Where(t => t.Id == report.TickerId)
            .Select(t => t.Symbol)
            .FirstOrDefaultAsync();

        // Fetch recent reports for trend analysis (up to 6 latest quarters)
        var recentReports = await _context.FinancialReports
            .Where(r => r.TickerId == report.TickerId && !r.IsDeleted)
            .OrderByDescending(r => r.ReportDate)
            .ThenByDescending(r => r.Year)
            .ThenByDescending(r => r.Quarter ?? 0)
            .Take(6)
            .ToListAsync();

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Bạn là trợ lý phân tích tài chính chuyên nghiệp.");
        contextBuilder.AppendLine("Bạn có quyền truy cập vào nhiều kỳ báo cáo tài chính để trả lờicâu hỏi.");
        contextBuilder.AppendLine("Hãy phân tích xu hướng, so sánh với kỳ trước và cùng kỳ năm trước nếu có thể.");
        contextBuilder.AppendLine("Trả lờingắn gọn, bằng tiếng Việt, chỉ dựa trên dữ liệu được cung cấp.");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine($"Mã cổ phiếu: {symbol ?? "N/A"}");
        contextBuilder.AppendLine();

        foreach (var r in recentReports.OrderByDescending(r => r.ReportDate))
        {
            var periodLabel = BuildPeriodLabel(r);
            contextBuilder.AppendLine($"--- Báo cáo: {periodLabel} ---");
            contextBuilder.AppendLine($"Ngày báo cáo: {r.ReportDate:yyyy-MM-dd}");
            contextBuilder.AppendLine("Nội dung:");
            contextBuilder.AppendLine(Cap(r.Content, 4000));
            contextBuilder.AppendLine();
        }

        var baseContext = contextBuilder.ToString().Trim();

        // Ingest documents for RAG (best-effort)
        foreach (var r in recentReports)
        {
            try
            {
                await _aiService.IngestDocumentAsync(
                    documentId: r.Id.ToString(),
                    source: "financial_report",
                    text: r.Content,
                    metadata: new
                    {
                        symbol,
                        reportType = r.ReportType,
                        year = r.Year,
                        quarter = r.Quarter,
                        reportDate = r.ReportDate
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ingest financial report {ReportId} for RAG", r.Id);
            }
        }

        var result = await _aiService.AnswerQuestionAsync(
            question: question,
            baseContext: baseContext,
            documentId: report.Id.ToString(),
            source: "financial_report",
            symbol: symbol,
            topK: 6);

        _logger.LogInformation("Answered question for report {ReportId} with {Count} recent reports", reportId, recentReports.Count);
        return result;
    }

    private static string BuildPeriodLabel(FinancialReport report)
    {
        if (report.Quarter.HasValue)
        {
            return $"Q{report.Quarter} {report.Year}";
        }

        return $"{report.Year} ({report.ReportType})";
    }

    private static bool TryParseSnapshot(string content, FinancialSnapshotDto snapshot)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            snapshot.Revenue = TryGetDecimal(doc.RootElement, "Revenue");
            snapshot.NetProfit = TryGetDecimal(doc.RootElement, "NetProfit");
            snapshot.Eps = TryGetDecimal(doc.RootElement, "EPS", "Eps");
            snapshot.Pe = TryGetDecimal(doc.RootElement, "PE", "P/E", "Pe");
            snapshot.Roe = TryGetDecimal(doc.RootElement, "ROE", "Roe");
            snapshot.DebtToEquity = TryGetDecimal(doc.RootElement, "DebtToEquity", "Debt/Equity", "DebtEquity");

            return snapshot.Revenue.HasValue ||
                   snapshot.NetProfit.HasValue ||
                   snapshot.Eps.HasValue ||
                   snapshot.Pe.HasValue ||
                   snapshot.Roe.HasValue ||
                   snapshot.DebtToEquity.HasValue;
        }
        catch
        {
            return false;
        }
    }

    private static decimal? TryGetDecimal(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
                {
                    return number;
                }

                if (element.ValueKind == JsonValueKind.String)
                {
                    var text = element.GetString();
                    if (decimal.TryParse(text, out var parsed))
                    {
                        return parsed;
                    }
                }
            }
        }

        return null;
    }

    private static string Cap(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + "…";
    }
}

