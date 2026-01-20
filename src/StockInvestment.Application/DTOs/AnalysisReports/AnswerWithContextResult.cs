namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Internal result model for backend services (no JsonPropertyName needed)
/// Used for communication between backend services only
/// </summary>
public class AnswerWithContextResult
{
    public string Answer { get; set; } = default!;
    public List<int> UsedSources { get; set; } = new();
}
