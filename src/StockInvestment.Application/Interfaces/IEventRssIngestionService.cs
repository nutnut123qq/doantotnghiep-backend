namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Ingests corporate events from configured RSS feeds into the database (and optionally RAG).
/// </summary>
public interface IEventRssIngestionService
{
    /// <summary>
    /// Returns the number of new events persisted.
    /// </summary>
    Task<int> IngestFromRssAsync(CancellationToken cancellationToken = default);
}
