using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.CorporateEvents.CreateEvent;

/// <summary>
/// Command to create a new corporate event
/// </summary>
public class CreateEventCommand : IRequest<CorporateEvent>
{
    public Guid StockTickerId { get; set; }
    public CorporateEventType EventType { get; set; }
    public DateTime EventDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceUrl { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Upcoming;
    
    // Event-specific data (for derived types)
    public Dictionary<string, object>? EventData { get; set; }
}
