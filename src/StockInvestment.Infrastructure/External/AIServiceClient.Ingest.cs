using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.AI;

namespace StockInvestment.Infrastructure.External;

public partial class AIServiceClient
{
    public async Task<IngestResult> IngestDocumentAsync(
        string documentId,
        string source,
        string text,
        object metadata,
        CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var endpoint = "/api/rag/ingest";
        var apiKey = Environment.GetEnvironmentVariable("AI_SERVICE_INTERNAL_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "AI_SERVICE_INTERNAL_API_KEY environment variable is not configured. " +
                "Set it to the internal API key shared with the AI service.");
        }

        var requestBody = new { document_id = documentId, source, text, metadata };
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(requestBody) };
        request.Headers.Add("X-Internal-Api-Key", apiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("AI service ingest failed ({StatusCode}): {Error}", response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<IngestResult>(cancellationToken: cancellationToken);
        if (result == null)
        {
            throw new Domain.Exceptions.ExternalServiceException("AI Service", "Failed to ingest document - response was null");
        }

        _logger.LogInformation("Successfully ingested document {DocumentId}: {ChunksUpserted} chunks", documentId, result.ChunksUpserted);
        return result;
    }
}
