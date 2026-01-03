using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Service interface for crawling corporate events
/// </summary>
public interface IEventCrawlerService
{
    /// <summary>
    /// Crawl upcoming corporate events from all sources
    /// </summary>
    Task<IEnumerable<CorporateEvent>> CrawlUpcomingEventsAsync(int daysAhead = 30);
    
    /// <summary>
    /// Crawl events for specific stock symbol
    /// </summary>
    Task<IEnumerable<CorporateEvent>> CrawlEventsBySymbolAsync(string symbol);
    
    /// <summary>
    /// Crawl events by event type
    /// </summary>
    Task<IEnumerable<CorporateEvent>> CrawlEventsByTypeAsync(CorporateEventType eventType);
}
