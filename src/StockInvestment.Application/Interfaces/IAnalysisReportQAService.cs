using StockInvestment.Application.DTOs.AnalysisReports;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Service for Q&A functionality on Analysis Reports
/// Builds context from report + financial data + news and calls AI service
/// </summary>
public interface IAnalysisReportQAService
{
    /// <summary>
    /// Ask a question about an analysis report
    /// Context is built from: report content, latest financials, recent news
    /// </summary>
    /// <param name="reportId">ID of the analysis report</param>
    /// <param name="question">User's question</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>QA result with answer and citations</returns>
    Task<QAResult> AskQuestionAsync(
        Guid reportId, 
        string question, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Internal result model for QA service
/// </summary>
public class QAResult
{
    public string Answer { get; set; } = default!;
    public List<CitationDto> Citations { get; set; } = new();
}
