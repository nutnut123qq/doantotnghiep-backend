using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IFinancialReportService
{
    Task<IEnumerable<FinancialReport>> GetReportsByTickerAsync(Guid tickerId);
    Task<IEnumerable<FinancialReport>> GetReportsBySymbolAsync(string symbol);
    Task<(IReadOnlyList<FinancialReport> Items, int TotalCount)> GetReportsForAdminAsync(int page = 1, int pageSize = 20, string? symbol = null);
    Task<FinancialSnapshotDto?> GetLatestFinancialSnapshotAsync(string symbol);
    Task<FinancialReport?> GetReportByIdAsync(Guid id);
    Task<bool> SetReportDeletedAsync(Guid id, bool isDeleted);
    Task<FinancialReport> AddReportAsync(FinancialReport report);
    Task<IEnumerable<FinancialReport>> AddReportsRangeAsync(IEnumerable<FinancialReport> reports);
    Task<QuestionAnswerResult> AskQuestionAsync(Guid reportId, string question);

    /// <summary>
    /// Crawls external sources for the symbol, dedupes by period key, persists new rows. Returns newly inserted reports.
    /// </summary>
    Task<IReadOnlyList<FinancialReport>> CrawlAndPersistReportsForSymbolAsync(string symbol, int maxReports, CancellationToken cancellationToken = default);
}

