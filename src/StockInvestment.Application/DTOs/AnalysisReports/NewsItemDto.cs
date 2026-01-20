namespace StockInvestment.Application.DTOs.AnalysisReports;

public class NewsItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string? Url { get; set; }
    public string? Summary { get; set; }
}
