using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// P2-1: Service for Analysis Reports to abstract DbContext usage from controllers
/// </summary>
public interface IAnalysisReportService
{
    Task<(IEnumerable<AnalysisReportListDto> Items, int Total)> GetReportsBySymbolAsync(
        string symbol,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AnalysisReport?> GetReportByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AnalysisReport> CreateReportAsync(AnalysisReport report, CancellationToken cancellationToken = default);
}
