namespace StockInvestment.Domain.Entities;

/// <summary>
/// Represents an analysis report from financial institutions (e.g., VNDirect, SSI)
/// V1: Stores Symbol as string only (NO TickerId FK) for simplicity
/// </summary>
public class AnalysisReport
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Stock symbol (e.g., "FPT", "VNM") - Indexed
    /// Normalized to uppercase for consistency
    /// </summary>
    public string Symbol { get; set; } = null!;
    
    /// <summary>
    /// Report title (e.g., "FPT - KQKD Q3/2025")
    /// </summary>
    public string Title { get; set; } = null!;
    
    /// <summary>
    /// Name of the financial institution/firm (e.g., "VNDirect", "SSI")
    /// </summary>
    public string FirmName { get; set; } = null!;
    
    /// <summary>
    /// Report publication date - Indexed
    /// </summary>
    public DateTime PublishedAt { get; set; }
    
    /// <summary>
    /// Investment recommendation: "Buy", "Hold", "Sell" (optional)
    /// </summary>
    public string? Recommendation { get; set; }
    
    /// <summary>
    /// Target price in VND (optional)
    /// </summary>
    public decimal? TargetPrice { get; set; }
    
    /// <summary>
    /// Full report content as plain text
    /// Stored as TEXT column (unlimited length)
    /// </summary>
    public string Content { get; set; } = null!;
    
    /// <summary>
    /// Optional source URL for reference (not for crawler in V1)
    /// </summary>
    public string? SourceUrl { get; set; }
    
    /// <summary>
    /// Record creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    public AnalysisReport()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}
