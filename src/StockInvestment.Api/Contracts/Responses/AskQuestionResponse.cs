namespace StockInvestment.Api.Contracts.Responses;

/// <summary>
/// API response for financial report Q&A
/// </summary>
public class AskQuestionResponse
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
}
