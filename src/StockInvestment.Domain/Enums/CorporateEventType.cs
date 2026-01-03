namespace StockInvestment.Domain.Entities;

/// <summary>
/// Types of corporate events
/// </summary>
public enum CorporateEventType
{
    /// <summary>
    /// Earnings announcement or quarterly/annual report
    /// </summary>
    Earnings = 1,
    
    /// <summary>
    /// Dividend announcement (cash or stock)
    /// </summary>
    Dividend = 2,
    
    /// <summary>
    /// Stock split or reverse split
    /// </summary>
    StockSplit = 3,
    
    /// <summary>
    /// Annual General Meeting
    /// </summary>
    AGM = 4,
    
    /// <summary>
    /// Rights issue or private placement
    /// </summary>
    RightsIssue = 5
}

/// <summary>
/// Status of a corporate event relative to current date
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// Event is scheduled but hasn't occurred yet
    /// </summary>
    Upcoming = 1,
    
    /// <summary>
    /// Event is happening today
    /// </summary>
    Today = 2,
    
    /// <summary>
    /// Event has already occurred
    /// </summary>
    Past = 3,
    
    /// <summary>
    /// Event was cancelled
    /// </summary>
    Cancelled = 4
}
