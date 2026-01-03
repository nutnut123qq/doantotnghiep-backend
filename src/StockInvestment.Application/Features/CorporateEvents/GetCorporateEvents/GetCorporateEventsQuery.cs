using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.CorporateEvents.GetCorporateEvents;

/// <summary>
/// Query to get corporate events with optional filtering
/// </summary>
public class GetCorporateEventsQuery : IRequest<IEnumerable<CorporateEvent>>
{
    /// <summary>
    /// Filter by stock symbol (optional)
    /// </summary>
    public string? Symbol { get; set; }
    
    /// <summary>
    /// Filter by event type (optional)
    /// </summary>
    public CorporateEventType? EventType { get; set; }
    
    /// <summary>
    /// Filter by start date (optional)
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// Filter by end date (optional)
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Filter by event status (optional)
    /// </summary>
    public EventStatus? Status { get; set; }
}
