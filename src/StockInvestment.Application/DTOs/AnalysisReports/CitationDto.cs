namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Citation item in Q&A response
/// P0 Fix #8: Includes CitationNumber field to match [n] in answer text
/// </summary>
public class CitationDto
{
    /// <summary>
    /// Original context index + 1 (matches [n] in answer text)
    /// P0 Fix #8: Frontend uses this directly, NOT reindex
    /// </summary>
    public int CitationNumber { get; set; }
    
    /// <summary>
    /// Type of source: "analysis_report", "financial_report", "news"
    /// </summary>
    public string SourceType { get; set; } = default!;
    
    /// <summary>
    /// Source entity ID (Guid as string)
    /// </summary>
    public string SourceId { get; set; } = default!;
    
    /// <summary>
    /// Title of the source document
    /// </summary>
    public string Title { get; set; } = default!;
    
    /// <summary>
    /// Optional URL to the source
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// Excerpt from the source (~200 chars, capped)
    /// </summary>
    public string Excerpt { get; set; } = default!;
}
