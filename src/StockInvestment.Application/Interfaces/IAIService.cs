namespace StockInvestment.Application.Interfaces;

public interface IAIService
{
    Task<string> SummarizeNewsAsync(string newsContent);
    Task<string> AnalyzeEventAsync(string eventDescription);
    Task<object> GenerateForecastAsync(Guid tickerId);
    Task<string> AnswerQuestionAsync(string question, string context);
    Task<object> ParseAlertIntentAsync(string naturalLanguageInput);
}

