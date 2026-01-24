using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface INewsService
{
    Task<IEnumerable<News>> GetNewsAsync(int page = 1, int pageSize = 20, Guid? tickerId = null);
    Task<News?> GetNewsByIdAsync(Guid id);
    Task<IReadOnlyList<NewsItemDto>> GetRecentNewsForSymbolAsync(string symbol, int days = 7, int limit = 5);
    Task RequestSummarizationAsync(Guid newsId);
    Task<News> AddNewsAsync(News news);
    Task<IEnumerable<News>> AddNewsRangeAsync(IEnumerable<News> newsList);
    Task UpdateNewsAsync(News news);
    /// <summary>
    /// Get all existing news URLs as a HashSet for efficient duplicate checking
    /// </summary>
    Task<HashSet<string>> GetExistingUrlsAsync();
}
