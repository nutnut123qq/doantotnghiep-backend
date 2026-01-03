using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.CorporateEvents.GetEventById;

/// <summary>
/// Query to get corporate event by ID
/// </summary>
public class GetEventByIdQuery : IRequest<CorporateEvent?>
{
    public Guid Id { get; set; }

    public GetEventByIdQuery(Guid id)
    {
        Id = id;
    }
}
