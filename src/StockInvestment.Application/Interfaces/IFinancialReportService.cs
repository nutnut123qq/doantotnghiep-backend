using StockInvestment.Application.Contracts.AI;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IFinancialReportService
{
    Task<IEnumerable<FinancialReport>> GetReportsByTickerAsync(Guid tickerId);
    Task<IEnumerable<FinancialReport>> GetReportsBySymbolAsync(string symbol);
    Task<FinancialReport?> GetReportByIdAsync(Guid id);
    Task<FinancialReport> AddReportAsync(FinancialReport report);
    Task<IEnumerable<FinancialReport>> AddReportsRangeAsync(IEnumerable<FinancialReport> reports);
    Task<QuestionAnswerResult> AskQuestionAsync(Guid reportId, string question);
}

