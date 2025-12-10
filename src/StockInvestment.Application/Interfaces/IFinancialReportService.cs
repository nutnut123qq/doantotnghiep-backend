using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IFinancialReportService
{
    Task<IEnumerable<FinancialReport>> GetReportsByTickerAsync(Guid tickerId);
    Task<FinancialReport?> GetReportByIdAsync(Guid id);
}

