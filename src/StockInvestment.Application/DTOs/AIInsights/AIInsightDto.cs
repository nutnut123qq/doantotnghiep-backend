namespace StockInvestment.Application.DTOs.AIInsights;

/// <summary>
/// DTO for AI Insight response
/// </summary>
public class AIInsightDto
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public List<string> Reasoning { get; set; } = new();
    public decimal? TargetPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime GeneratedAt { get; set; }
}
