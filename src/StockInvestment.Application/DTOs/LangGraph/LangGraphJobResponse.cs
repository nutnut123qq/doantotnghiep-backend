using System.Text.Json.Serialization;

namespace StockInvestment.Application.DTOs.LangGraph;

/// <summary>
/// Response from Python <c>POST /api/analyze/enqueue</c> or
/// <c>GET /api/analyze/jobs/{id}</c>. Covers three lifecycle states:
/// <list type="bullet">
///   <item><description><c>queued</c> / <c>running</c>: only <see cref="JobId"/> and <see cref="Status"/> are populated.</description></item>
///   <item><description><c>completed</c>: <see cref="Result"/> contains the forecast payload.</description></item>
///   <item><description><c>failed</c>: <see cref="Error"/> contains a short last-line of the worker traceback.</description></item>
/// </list>
/// </summary>
public sealed class LangGraphJobResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("jobId")]
    public string? JobId { get; set; }

    [JsonPropertyName("result")]
    public LangGraphAnalyzeResponse? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
