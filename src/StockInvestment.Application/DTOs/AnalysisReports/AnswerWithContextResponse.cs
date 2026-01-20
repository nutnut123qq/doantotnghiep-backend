using System.Text.Json.Serialization;

namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Response from AI service /api/ai/answer-with-context endpoint
/// CRITICAL: Uses snake_case via JsonPropertyName for Python Pydantic compatibility (P0 Fix #1)
/// </summary>
public sealed class AnswerWithContextResponse
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; } = default!;
    
    [JsonPropertyName("used_sources")]
    public List<int> UsedSources { get; set; } = new();
}
