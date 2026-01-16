namespace StockInvestment.Application.DTOs.AIInsights;

/// <summary>
/// Request DTO for generating AI insight
/// </summary>
public class GenerateInsightRequest
{
    public string Symbol { get; set; } = string.Empty;
}
