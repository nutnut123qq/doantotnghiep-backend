namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Builds plain-text context strings for external AI analyst services (e.g. Python LangGraph).
/// </summary>
public interface IAnalystContextService
{
    Task<string> BuildNewsContextAsync(
        string symbol,
        int topK,
        int lookbackDays,
        CancellationToken cancellationToken = default);

    Task<string> BuildTechContextAsync(
        string symbol,
        int barLimit,
        CancellationToken cancellationToken = default);
}
