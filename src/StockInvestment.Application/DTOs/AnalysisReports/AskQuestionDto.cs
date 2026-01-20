using System.ComponentModel.DataAnnotations;

namespace StockInvestment.Application.DTOs.AnalysisReports;

/// <summary>
/// Request DTO for POST /api/analysis-reports/{id}/qa
/// P0 Fix #11: Validation attributes to prevent empty questions
/// </summary>
public class AskQuestionDto
{
    [Required(ErrorMessage = "Question is required")]
    [MinLength(3, ErrorMessage = "Question must be at least 3 characters")]
    public string Question { get; set; } = default!;
}
