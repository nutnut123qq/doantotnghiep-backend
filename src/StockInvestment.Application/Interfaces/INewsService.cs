using StockInvestment.Application.DTOs.Common;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface INewsService
{
    Task<(IReadOnlyList<News> Items, int TotalCount)> GetNewsAsync(int page = 1, int pageSize = 20, Guid? tickerId = null);
    /// <summary>All news including soft-deleted (admin list).</summary>
    Task<(IReadOnlyList<News> Items, int TotalCount)> GetNewsForAdminAsync(int page = 1, int pageSize = 20, Guid? tickerId = null);
    Task<News?> GetNewsByIdAsync(Guid id);
    Task<IReadOnlyList<NewsItemDto>> GetRecentNewsForSymbolAsync(string symbol, int days = 7, int limit = 5);
    Task RequestSummarizationAsync(Guid newsId);
    Task<News> AddNewsAsync(News news);
    Task<IEnumerable<News>> AddNewsRangeAsync(IEnumerable<News> newsList);
    Task UpdateNewsAsync(News news);
    /// <summary>Sets soft-delete flag. Returns false if news id does not exist.</summary>
    Task<bool> SetNewsDeletedAsync(Guid id, bool isDeleted);
    /// <summary>
    /// Get all existing news URLs as a HashSet for efficient duplicate checking
    /// </summary>
    Task<HashSet<string>> GetExistingUrlsAsync();
    Task<HashSet<string>> GetExistingFingerprintsAsync();
    Task<QuestionAnswerResult> AskQuestionAsync(string? symbol, string question, int days = 7, int topK = 6);
}
