namespace StockInvestment.Domain.Entities;

/// <summary>
/// Base entity for all corporate events (earnings, dividends, splits, AGM, rights issue)
/// </summary>
public abstract class CorporateEvent
{
    public Guid Id { get; set; }
    public Guid StockTickerId { get; set; }
    public virtual StockTicker StockTicker { get; set; } = null!;
    
    /// <summary>
    /// Type of event: Earnings, Dividend, StockSplit, AGM, RightsIssue
    /// </summary>
    public CorporateEventType EventType { get; set; }
    
    /// <summary>
    /// Date when the event occurs or occurred
    /// </summary>
    public DateTime EventDate { get; set; }
    
    /// <summary>
    /// Event title/name
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of the event
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Source URL where the event was announced
    /// </summary>
    public string? SourceUrl { get; set; }
    
    /// <summary>
    /// Indicates if this is a past, current, or upcoming event
    /// </summary>
    public EventStatus Status { get; set; }
    
    /// <summary>
    /// Additional data stored as JSON (event-specific fields)
    /// </summary>
    public string? AdditionalData { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    protected CorporateEvent()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Status = EventStatus.Upcoming;
    }
}

/// <summary>
/// Earnings announcement/report event
/// </summary>
public class EarningsEvent : CorporateEvent
{
    /// <summary>
    /// Quarter (Q1, Q2, Q3, Q4) or Year
    /// </summary>
    public string Period { get; set; } = string.Empty;
    
    /// <summary>
    /// Fiscal year
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Earnings per share (EPS)
    /// </summary>
    public decimal? EPS { get; set; }
    
    /// <summary>
    /// Revenue in VND
    /// </summary>
    public decimal? Revenue { get; set; }
    
    /// <summary>
    /// Net profit in VND
    /// </summary>
    public decimal? NetProfit { get; set; }
    
    public EarningsEvent()
    {
        EventType = CorporateEventType.Earnings;
    }
}

/// <summary>
/// Dividend announcement event
/// </summary>
public class DividendEvent : CorporateEvent
{
    /// <summary>
    /// Dividend per share (cash or stock)
    /// </summary>
    public decimal DividendPerShare { get; set; }
    
    /// <summary>
    /// Cash dividend in VND
    /// </summary>
    public decimal? CashDividend { get; set; }
    
    /// <summary>
    /// Stock dividend ratio (e.g., 10% means 1 share for every 10 owned)
    /// </summary>
    public decimal? StockDividendRatio { get; set; }
    
    /// <summary>
    /// Ex-dividend date (when stock starts trading without dividend)
    /// </summary>
    public DateTime? ExDividendDate { get; set; }
    
    /// <summary>
    /// Record date (date to be shareholder to receive dividend)
    /// </summary>
    public DateTime? RecordDate { get; set; }
    
    /// <summary>
    /// Payment date (when dividend is paid)
    /// </summary>
    public DateTime? PaymentDate { get; set; }
    
    public DividendEvent()
    {
        EventType = CorporateEventType.Dividend;
    }
}

/// <summary>
/// Stock split or reverse split event
/// </summary>
public class StockSplitEvent : CorporateEvent
{
    /// <summary>
    /// Split ratio (e.g., "2:1" means 2 new shares for 1 old share)
    /// </summary>
    public string SplitRatio { get; set; } = string.Empty;
    
    /// <summary>
    /// Is this a reverse split (consolidation)?
    /// </summary>
    public bool IsReverseSplit { get; set; }
    
    /// <summary>
    /// Effective date of the split
    /// </summary>
    public DateTime EffectiveDate { get; set; }
    
    /// <summary>
    /// Record date for the split
    /// </summary>
    public DateTime? RecordDate { get; set; }
    
    public StockSplitEvent()
    {
        EventType = CorporateEventType.StockSplit;
    }
}

/// <summary>
/// Annual General Meeting event
/// </summary>
public class AGMEvent : CorporateEvent
{
    /// <summary>
    /// Meeting location/venue
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// Meeting time
    /// </summary>
    public TimeSpan? MeetingTime { get; set; }
    
    /// <summary>
    /// Meeting agenda items (JSON array)
    /// </summary>
    public string? Agenda { get; set; }
    
    /// <summary>
    /// Record date to be eligible to attend
    /// </summary>
    public DateTime? RecordDate { get; set; }
    
    /// <summary>
    /// Year of the AGM (e.g., AGM 2024)
    /// </summary>
    public int Year { get; set; }
    
    public AGMEvent()
    {
        EventType = CorporateEventType.AGM;
    }
}

/// <summary>
/// Rights issue or private placement event
/// </summary>
public class RightsIssueEvent : CorporateEvent
{
    /// <summary>
    /// Number of new shares to be issued
    /// </summary>
    public long NumberOfShares { get; set; }
    
    /// <summary>
    /// Issue price per share
    /// </summary>
    public decimal IssuePrice { get; set; }
    
    /// <summary>
    /// Rights ratio (e.g., "1:2" means 1 new share for every 2 existing)
    /// </summary>
    public string? RightsRatio { get; set; }
    
    /// <summary>
    /// Record date to be eligible for rights
    /// </summary>
    public DateTime? RecordDate { get; set; }
    
    /// <summary>
    /// Subscription start date
    /// </summary>
    public DateTime? SubscriptionStartDate { get; set; }
    
    /// <summary>
    /// Subscription end date
    /// </summary>
    public DateTime? SubscriptionEndDate { get; set; }
    
    /// <summary>
    /// Purpose of the rights issue
    /// </summary>
    public string? Purpose { get; set; }
    
    public RightsIssueEvent()
    {
        EventType = CorporateEventType.RightsIssue;
    }
}
