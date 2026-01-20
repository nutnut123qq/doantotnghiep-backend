using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Services;

public class FinancialReportService : IFinancialReportService
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIService _aiService;
    private readonly ILogger<FinancialReportService> _logger;

    public FinancialReportService(
        ApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IAIService aiService,
        ILogger<FinancialReportService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<IEnumerable<FinancialReport>> GetReportsByTickerAsync(Guid tickerId)
    {
        return await _context.FinancialReports
            .Where(r => r.TickerId == tickerId)
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
            .Where(r => r.TickerId == ticker.Id)
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
        return await _unitOfWork.Repository<FinancialReport>()
            .GetByIdAsync(id);
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

    public async Task<QuestionAnswerResult> AskQuestionAsync(Guid reportId, string question)
    {
        var report = await GetReportByIdAsync(reportId);
        if (report == null)
        {
            throw new Domain.Exceptions.NotFoundException("FinancialReport", reportId);
        }

        var context = $"Financial Report - {report.ReportType} {report.Year}";
        if (report.Quarter.HasValue)
        {
            context += $" Q{report.Quarter}";
        }
        context += $"\n\nData:\n{report.Content}";

        var result = await _aiService.AnswerQuestionAsync(question, context);
        
        _logger.LogInformation("Answered question for report {ReportId}", reportId);
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

