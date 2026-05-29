using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.DTOs.AIInsights;

public class GenerateInsightsBatchResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<Guid> FailedTickerIds { get; set; } = new();
    public IReadOnlyList<AIInsight> Insights { get; set; } = Array.Empty<AIInsight>();
}
