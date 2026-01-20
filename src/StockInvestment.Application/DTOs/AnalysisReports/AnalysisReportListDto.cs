namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// List item DTO for GET /api/analysis-reports (NO full content)
/// </summary>
public class AnalysisReportListDto
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string FirmName { get; set; } = default!;
    public DateTime PublishedAt { get; set; }
    public string? Recommendation { get; set; }
    public decimal? TargetPrice { get; set; }
    public string? SourceUrl { get; set; }
    
    /// <summary>
    /// Content preview (first 200 chars, capped)
    /// P0 Fix #2: Safe capping to avoid full content in list
    /// </summary>
    public string ContentPreview { get; set; } = default!;
}
