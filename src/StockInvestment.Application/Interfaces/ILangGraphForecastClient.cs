using StockInvestment.Application.DTOs.LangGraph;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Calls the Python LangGraph Stock Analyst endpoints on <c>/api/analyze*</c>.
/// </summary>
public interface ILangGraphForecastClient
{
    /// <summary>
    /// Legacy synchronous call — invokes <c>POST /api/analyze</c> and blocks until the graph finishes.
    /// Kept for backward compatibility; prefer the enqueue/poll pair below for dashboard flows.
    /// </summary>
    Task<LangGraphAnalyzeResponse?> AnalyzeAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueue an analyse job on the Python RQ worker.
    /// Python returns HTTP 200 with <c>status=completed</c> on cache hit, or HTTP 202 with <c>status=queued</c>.
    /// </summary>
    Task<LangGraphJobResponse?> EnqueueAnalyzeAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Poll the status of a previously enqueued analyse job.
    /// Returns null when Python responds 404 (job expired / unknown id).
    /// </summary>
    Task<LangGraphJobResponse?> GetAnalyzeJobAsync(string jobId, CancellationToken cancellationToken = default);
}
