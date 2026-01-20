using System.Text.Json.Serialization;

namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Context part for AI service communication
/// CRITICAL: Uses snake_case via JsonPropertyName for Python Pydantic compatibility (P0 Fix #1)
/// </summary>
public sealed class ContextPart
{
    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = default!;
    
    [JsonPropertyName("source_id")]
    public string SourceId { get; set; } = default!;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;
    
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    
    [JsonPropertyName("excerpt")]
    public string Excerpt { get; set; } = default!;
    
    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }
}
