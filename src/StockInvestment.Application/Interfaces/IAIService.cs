using StockInvestment.Application.Contracts.AI;

namespace StockInvestment.Application.Interfaces;

public interface IAIService
{
    Task<string> SummarizeNewsAsync(string newsContent, CancellationToken cancellationToken = default);
    Task<NewsSummaryResult> SummarizeNewsDetailedAsync(string newsContent, CancellationToken cancellationToken = default);
    Task<string> AnalyzeEventAsync(string eventDescription, CancellationToken cancellationToken = default);
    Task<EventAnalysisResult> AnalyzeEventDetailedAsync(string eventDescription, CancellationToken cancellationToken = default);
    Task<object> GenerateForecastAsync(Guid tickerId, CancellationToken cancellationToken = default);
    Task<ForecastResult> GenerateForecastBySymbolAsync(string symbol, string timeHorizon = "short", CancellationToken cancellationToken = default);
    Task<ForecastResult> GenerateForecastWithDataAsync(string symbol, string timeHorizon, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default);
    Task<QuestionAnswerResult> AnswerQuestionAsync(string question, string context, CancellationToken cancellationToken = default);
    Task<ParsedAlert> ParseAlertAsync(string naturalLanguageInput, CancellationToken cancellationToken = default);
    Task<InsightResult> GenerateInsightAsync(string symbol, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default);
    Task<string?> GetAlertExplanationAsync(string symbol, string alertType, decimal currentValue, decimal threshold, CancellationToken cancellationToken = default);
}

public class ParsedAlert
{
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Timeframe { get; set; }
}

public class ForecastResult
{
    public string Symbol { get; set; } = string.Empty;
    public string Trend { get; set; } = string.Empty; // Up, Down, Sideways
    public string Confidence { get; set; } = string.Empty; // High, Medium, Low
    public double ConfidenceScore { get; set; }
    public string TimeHorizon { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty; // Buy, Hold, Sell
    public List<string> KeyDrivers { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public string Analysis { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

public class InsightResult
{
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Buy, Sell, Hold
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Confidence { get; set; } // 0-100
    public List<string> Reasoning { get; set; } = new();
    public decimal? TargetPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class NewsSummaryResult
{
    public string Summary { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty; // positive, negative, neutral
    public string ImpactAssessment { get; set; } = string.Empty;
    public List<string>? KeyPoints { get; set; }
}

public class EventAnalysisResult
{
    public string Analysis { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}

