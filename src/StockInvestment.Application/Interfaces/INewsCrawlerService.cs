using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface INewsCrawlerService
{
    /// <summary>
    /// Crawl news from all configured sources
    /// </summary>
    Task<IEnumerable<News>> CrawlNewsAsync(int maxArticles = 20);
    
    /// <summary>
    /// Crawl news for a specific stock symbol
    /// </summary>
    Task<IEnumerable<News>> CrawlNewsBySymbolAsync(string symbol, int maxArticles = 10);
    
    /// <summary>
    /// Crawl news from a specific source
    /// </summary>
    Task<IEnumerable<News>> CrawlFromSourceAsync(string source, int maxArticles = 20);
}

