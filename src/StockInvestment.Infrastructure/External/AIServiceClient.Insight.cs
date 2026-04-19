using System.Net.Http.Json;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.External;

public partial class AIServiceClient
{
    public async Task<InsightResult> GenerateInsightAsync(string symbol, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            symbol,
            technical_data = technicalData ?? new Dictionary<string, string>(),
            fundamental_data = fundamentalData ?? new Dictionary<string, string>(),
            sentiment_data = sentimentData ?? new Dictionary<string, string>()
        };

        // Use dedicated client with long timeout so slow LLM calls (Beeknoee rate-limit
        // queueing) do not trip the default AIServiceClient 120s timeout.
        var insightClient = _httpClientFactory.CreateClient("AIInsightService");
        if (insightClient.BaseAddress == null)
        {
            throw new InvalidOperationException("AIInsightService HttpClient BaseAddress is not configured. Please check AIService configuration.");
        }

        var endpoint = "/api/insights/generate";
        var response = await insightClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleHttpErrorAsync(response, endpoint, symbol);
        }

        var result = await response.Content.ReadFromJsonAsync<InsightApiResponse>(cancellationToken: cancellationToken);
        if (result == null)
        {
            throw new Domain.Exceptions.ExternalServiceException("AI Service", "Failed to get insight from AI service - response was null");
        }

        return new InsightResult
        {
            Symbol = result.Symbol,
            Type = result.Type,
            Title = result.Title,
            Description = result.Description,
            Confidence = result.Confidence,
            Reasoning = result.Reasoning,
            TargetPrice = result.TargetPrice,
            StopLoss = result.StopLoss,
            GeneratedAt = ParseGeneratedAt(result.GeneratedAt),
            Evidence = result.Evidence,
            Metadata = result.Metadata
        };
    }
}
