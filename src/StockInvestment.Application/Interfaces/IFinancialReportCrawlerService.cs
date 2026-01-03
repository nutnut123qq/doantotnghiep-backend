using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IFinancialReportCrawlerService
{
    /// <summary>
    /// Crawl financial reports for a specific ticker symbol
    /// </summary>
    Task<IEnumerable<FinancialReport>> CrawlReportsBySymbolAsync(string symbol, int maxReports = 10);

    /// <summary>
    /// Crawl latest financial reports from VietStock
    /// </summary>
    Task<IEnumerable<FinancialReport>> CrawlFromVietStockAsync(string symbol, int maxReports = 5);

    /// <summary>
    /// Crawl latest financial reports from CafeF
    /// </summary>
    Task<IEnumerable<FinancialReport>> CrawlFromCafeFAsync(string symbol, int maxReports = 5);
}

