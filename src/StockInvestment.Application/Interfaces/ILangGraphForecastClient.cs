using StockInvestment.Application.DTOs.LangGraph;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Calls the Python LangGraph Stock Analyst <c>POST /api/analyze</c>.
/// </summary>
public interface ILangGraphForecastClient
{
    /// <summary>
    /// Invokes analysis; empty news/tech context lets the graph fetch from .NET AnalystContext endpoints.
    /// </summary>
    Task<LangGraphAnalyzeResponse?> AnalyzeAsync(string symbol, CancellationToken cancellationToken = default);
}
