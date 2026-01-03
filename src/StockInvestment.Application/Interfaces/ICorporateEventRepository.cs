using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Repository interface for corporate events
/// </summary>
public interface ICorporateEventRepository
{
    /// <summary>
    /// Get all events with optional filtering
    /// </summary>
    Task<IEnumerable<CorporateEvent>> GetAllAsync(
        string? symbol = null,
        CorporateEventType? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        EventStatus? status = null);
    
    /// <summary>
    /// Get event by ID
    /// </summary>
    Task<CorporateEvent?> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get events by stock ticker
    /// </summary>
    Task<IEnumerable<CorporateEvent>> GetByTickerAsync(Guid stockTickerId);
    
    /// <summary>
    /// Get upcoming events (future events only)
    /// </summary>
    Task<IEnumerable<CorporateEvent>> GetUpcomingEventsAsync(int daysAhead = 30);
    
    /// <summary>
    /// Get events by date range
    /// </summary>
    Task<IEnumerable<CorporateEvent>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// Get events by type
    /// </summary>
    Task<IEnumerable<CorporateEvent>> GetByTypeAsync(CorporateEventType eventType);
    
    /// <summary>
    /// Create new event
    /// </summary>
    Task<CorporateEvent> CreateAsync(CorporateEvent corporateEvent);
    
    /// <summary>
    /// Update existing event
    /// </summary>
    Task UpdateAsync(CorporateEvent corporateEvent);
    
    /// <summary>
    /// Delete event
    /// </summary>
    Task DeleteAsync(Guid id);
    
    /// <summary>
    /// Check if event exists for ticker on specific date
    /// </summary>
    Task<bool> ExistsAsync(Guid stockTickerId, CorporateEventType eventType, DateTime eventDate);
}
