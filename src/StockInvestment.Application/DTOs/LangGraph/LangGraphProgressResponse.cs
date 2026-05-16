using System.Text.Json.Serialization;

namespace StockInvestment.Application.DTOs.LangGraph;

/// <summary>
/// A single thinking step emitted by a LangGraph node.
/// </summary>
public sealed class LangGraphProgressStep
{
    [JsonPropertyName("node")]
    public string? Node { get; set; }

    [JsonPropertyName("output")]
    public Dictionary<string, object>? Output { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

/// <summary>
/// Response from Python <c>GET /api/analyze/progress/{job_id}</c>.
/// </summary>
public sealed class LangGraphProgressResponse
{
    [JsonPropertyName("jobId")]
    public string? JobId { get; set; }

    [JsonPropertyName("steps")]
    public List<LangGraphProgressStep>? Steps { get; set; }
}
