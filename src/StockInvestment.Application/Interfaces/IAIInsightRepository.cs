using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Repository interface for AI Insights
/// </summary>
public interface IAIInsightRepository : IRepository<AIInsight>
{
    /// <summary>
    /// Get insights with optional filters
    /// </summary>
    Task<IEnumerable<AIInsight>> GetInsightsAsync(
        InsightType? type = null,
        string? symbol = null,
        bool includeDismissed = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get insight by ID with Ticker navigation property
    /// </summary>
    Task<AIInsight?> GetInsightByIdWithTickerAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find existing active insight for ticker and type
    /// </summary>
    Task<AIInsight?> FindActiveInsightAsync(
        Guid tickerId,
        InsightType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all non-dismissed insights for market sentiment calculation
    /// </summary>
    Task<IEnumerable<AIInsight>> GetNonDismissedInsightsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get old dismissed insights for cleanup
    /// </summary>
    Task<IEnumerable<AIInsight>> GetOldDismissedInsightsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
