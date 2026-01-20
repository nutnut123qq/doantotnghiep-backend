namespace StockInvestment.Application.Contracts.AI;

/// <summary>
/// Result from AI Q&A service containing answer and source objects (RAG)
/// </summary>
public class QuestionAnswerResult
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceObject> Sources { get; set; } = new();
}
