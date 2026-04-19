using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Tests.Mocks;

/// <summary>
/// Stub IAIService for integration tests (no external HTTP calls).
/// </summary>
public sealed class MockAIService : IAIService
{
    private static HttpRequestException BuildHttpException(int statusCode, string message)
    {
        var ex = new HttpRequestException(message);
        ex.Data["StatusCode"] = statusCode;
        return ex;
    }

    public Task<string> SummarizeNewsAsync(string newsContent, CancellationToken cancellationToken = default)
        => Task.FromResult("Summary");

    public Task<NewsSummaryResult> SummarizeNewsDetailedAsync(string newsContent, CancellationToken cancellationToken = default)
        => Task.FromResult(new NewsSummaryResult { Summary = "S", Sentiment = "neutral", ImpactAssessment = "" });

    public Task<object> GenerateForecastAsync(Guid tickerId, CancellationToken cancellationToken = default)
        => Task.FromResult<object>(new ForecastResult { Symbol = "X", Trend = "Sideways", TimeHorizon = "short" });

    public Task<ForecastResult> GenerateForecastBySymbolAsync(string symbol, string timeHorizon = "short", CancellationToken cancellationToken = default)
    {
        if (symbol.Equals("ERR429", StringComparison.OrdinalIgnoreCase))
        {
            throw BuildHttpException(429, "Quota exceeded");
        }

        if (symbol.Equals("ERR500", StringComparison.OrdinalIgnoreCase))
        {
            throw BuildHttpException(500, "Internal AI service error");
        }

        return Task.FromResult(new ForecastResult
        {
            Symbol = symbol,
            Trend = "Sideways",
            TimeHorizon = timeHorizon,
            Confidence = "Medium",
            Recommendation = "Hold"
        });
    }

    public Task<ForecastResult> GenerateForecastWithDataAsync(string symbol, string timeHorizon, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default)
    {
        if (symbol.Equals("ERR429", StringComparison.OrdinalIgnoreCase))
        {
            throw BuildHttpException(429, "Quota exceeded");
        }

        if (symbol.Equals("ERR500", StringComparison.OrdinalIgnoreCase))
        {
            throw BuildHttpException(500, "Internal AI service error");
        }

        return Task.FromResult(new ForecastResult
        {
            Symbol = symbol,
            Trend = "Sideways",
            TimeHorizon = timeHorizon,
            Confidence = "Medium",
            Recommendation = "Hold"
        });
    }

    public Task<QuestionAnswerResult> AnswerQuestionAsync(string question, string baseContext, string? documentId = null, string? source = null, string? symbol = null, int topK = 6, CancellationToken cancellationToken = default)
        => Task.FromResult(new QuestionAnswerResult { Answer = "", Sources = new List<SourceObject>() });

    public Task<IngestResult> IngestDocumentAsync(string documentId, string source, string text, object metadata, CancellationToken cancellationToken = default)
        => Task.FromResult(new IngestResult { DocumentId = documentId, ChunksUpserted = 0, Collection = "", EmbeddingModel = "" });

    public Task<InsightResult> GenerateInsightAsync(string symbol, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default)
        => Task.FromResult(new InsightResult { Symbol = symbol, Type = "Hold", Title = "", Description = "" });
}
