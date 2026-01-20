namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Response DTO for POST /api/analysis-reports/{id}/qa
/// </summary>
public class QAResponseDto
{
    /// <summary>
    /// AI-generated answer with inline citations like [1] [2]
    /// </summary>
    public string Answer { get; set; } = default!;
    
    /// <summary>
    /// List of citations used in the answer
    /// P0 Fix #8: Each citation has CitationNumber matching [n] in answer
    /// </summary>
    public List<CitationDto> Citations { get; set; } = new();
}
