using System.Net.Http.Json;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.External;

public partial class AIServiceClient
{
    public async Task<string> SummarizeNewsAsync(string newsContent, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/summarize", new { content = newsContent }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SummarizeResponse>(cancellationToken: cancellationToken);
        return result?.Summary ?? string.Empty;
    }

    public async Task<NewsSummaryResult> SummarizeNewsDetailedAsync(string newsContent, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/summarize", new { content = newsContent }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SummarizeDetailedResponse>(cancellationToken: cancellationToken);
        return new NewsSummaryResult
        {
            Summary = result?.Summary ?? string.Empty,
            Sentiment = result?.Sentiment ?? "neutral",
            ImpactAssessment = result?.ImpactAssessment ?? result?.Impact_Assessment ?? string.Empty,
            KeyPoints = result?.KeyPoints ?? result?.Key_Points ?? new List<string>()
        };
    }

}
