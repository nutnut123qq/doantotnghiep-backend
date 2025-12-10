using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface INewsService
{
    Task<IEnumerable<News>> GetNewsAsync(int page = 1, int pageSize = 20, Guid? tickerId = null);
    Task<News?> GetNewsByIdAsync(Guid id);
    Task RequestSummarizationAsync(Guid newsId);
}
