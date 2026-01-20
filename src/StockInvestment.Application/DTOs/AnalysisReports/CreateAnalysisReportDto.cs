using System.ComponentModel.DataAnnotations;

namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Request DTO for POST /api/analysis-reports
/// V1: Plain text content only (NO URL crawler)
/// </summary>
public class CreateAnalysisReportDto
{
    [Required(ErrorMessage = "Symbol is required")]
    public string Symbol { get; set; } = default!;
    
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = default!;
    
    [Required(ErrorMessage = "Firm name is required")]
    public string FirmName { get; set; } = default!;
    
    [Required(ErrorMessage = "Published date is required")]
    public DateTime PublishedAt { get; set; }
    
    /// <summary>
    /// Investment recommendation: "Buy", "Hold", "Sell"
    /// </summary>
    public string? Recommendation { get; set; }
    
    /// <summary>
    /// Target price in VND
    /// </summary>
    public decimal? TargetPrice { get; set; }
    
    /// <summary>
    /// Full report content as plain text (required in V1)
    /// </summary>
    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = default!;
    
    /// <summary>
    /// Optional source URL for reference only (NOT for crawler in V1)
    /// P0 Fix #6: No dto.Url field in V1
    /// </summary>
    public string? SourceUrl { get; set; }
}
