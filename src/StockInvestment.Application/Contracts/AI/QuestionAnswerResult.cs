namespace StockInvestment.Application.Contracts.AI;

/// <summary>
/// Result from AI Q&A service containing answer and citation sources
/// </summary>
public class QuestionAnswerResult
{
    public string Answer { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
}
