using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.CorporateEvents.GetUpcomingEvents;

/// <summary>
/// Query to get upcoming corporate events
/// </summary>
public class GetUpcomingEventsQuery : IRequest<IEnumerable<CorporateEvent>>
{
    /// <summary>
    /// Number of days ahead to look for events (default: 30)
    /// </summary>
    public int DaysAhead { get; set; } = 30;
}
