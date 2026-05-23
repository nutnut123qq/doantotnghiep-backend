namespace StockInvestment.Application.DTOs.AIInsights;

/// <summary>
/// Request DTO for batch generating AI insights
/// </summary>
public class GenerateInsightsBatchRequest
{
    /// <summary>
    /// Optional list of symbols to generate insights for.
    /// When empty or null, defaults to VN30 universe.
    /// </summary>
    public List<string>? Symbols { get; set; }
}
