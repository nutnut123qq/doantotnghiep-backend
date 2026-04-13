using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

internal static class CorporateEventRagHelper
{
    public const string RagSource = "corporate_event";

    public static async Task TryIngestForRagAsync(
        IAIService aiService,
        CorporateEvent ev,
        string symbol,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var text = $"{ev.Title}\n{ev.Description}\nType: {ev.EventType}\nDate: {ev.EventDate:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(ev.AdditionalData))
            text += $"\n{ev.AdditionalData}";

        try
        {
            await aiService.IngestDocumentAsync(
                documentId: ev.Id.ToString(),
                source: RagSource,
                text: text,
                metadata: new
                {
                    symbol,
                    title = ev.Title,
                    sourceUrl = ev.SourceUrl,
                    eventDate = ev.EventDate,
                    eventType = ev.EventType.ToString()
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ingest corporate event {EventId} for RAG", ev.Id);
        }
    }
}
