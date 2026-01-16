using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Interfaces;

public interface IAIInsightService
{
    Task<IEnumerable<AIInsight>> GetInsightsAsync(
        InsightType? type = null,
        string? symbol = null,
        bool includeDismissed = false,
        CancellationToken cancellationToken = default);

    Task<AIInsight?> GetInsightByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AIInsight> GenerateInsightAsync(
        Guid tickerId,
        Dictionary<string, string>? technicalData = null,
        Dictionary<string, string>? fundamentalData = null,
        Dictionary<string, string>? sentimentData = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<AIInsight>> GenerateInsightsBatchAsync(
        IEnumerable<Guid> tickerIds,
        CancellationToken cancellationToken = default);

    Task DismissInsightAsync(Guid insightId, Guid userId, CancellationToken cancellationToken = default);

    Task<MarketSentiment> GetMarketSentimentAsync(CancellationToken cancellationToken = default);

    Task CleanupOldDismissedInsightsAsync(int daysOld = 7, CancellationToken cancellationToken = default);
}

public class MarketSentiment
{
    public string Overall { get; set; } = string.Empty; // Bullish, Bearish, Neutral
    public int Score { get; set; } // 0-100
    public int BuySignalsCount { get; set; }
    public int SellSignalsCount { get; set; }
    public int HoldSignalsCount { get; set; }
    public int OpportunitiesCount { get; set; } // Buy signals with confidence > 70
    public string RiskLevel { get; set; } = string.Empty; // Low, Moderate, High
    public int VolatilityIndex { get; set; } // 0-100
}
