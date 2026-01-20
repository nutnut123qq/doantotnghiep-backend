namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Detail DTO for GET /api/analysis-reports/{id} (with FULL content)
/// </summary>
public class AnalysisReportDetailDto
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string FirmName { get; set; } = default!;
    public DateTime PublishedAt { get; set; }
    public string? Recommendation { get; set; }
    public decimal? TargetPrice { get; set; }
    
    /// <summary>
    /// Full report content (plain text)
    /// </summary>
    public string Content { get; set; } = default!;
    
    public string? SourceUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
