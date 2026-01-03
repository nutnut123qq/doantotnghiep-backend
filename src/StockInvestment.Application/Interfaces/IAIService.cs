namespace StockInvestment.Application.Interfaces;

public interface IAIService
{
    Task<string> SummarizeNewsAsync(string newsContent);
    Task<string> AnalyzeEventAsync(string eventDescription);
    Task<object> GenerateForecastAsync(Guid tickerId);
    Task<ForecastResult> GenerateForecastBySymbolAsync(string symbol, string timeHorizon = "short");
    Task<string> AnswerQuestionAsync(string question, string context);
    Task<ParsedAlert> ParseAlertAsync(string naturalLanguageInput);
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

