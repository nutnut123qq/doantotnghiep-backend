namespace StockInvestment.Api.Contracts.Responses;

/// <summary>
/// Unified API response for Q&A endpoints.
/// </summary>
public class AskQuestionResponse
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<QASourceResponse> Sources { get; set; } = new();
}

/// <summary>
/// Source item attached to Q&A answer for citation rendering.
/// </summary>
public class QASourceResponse
{
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? PublishedAt { get; set; }
}
