using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Read technical indicators persisted by background jobs (no VNStock on read path).
/// </summary>
public interface ITechnicalIndicatorQueryService
{
    /// <summary>
    /// Returns latest stored indicator rows for the symbol (VN30 ticker must exist in DB).
    /// </summary>
    Task<IReadOnlyList<TechnicalIndicator>> GetLatestStoredIndicatorsAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}
