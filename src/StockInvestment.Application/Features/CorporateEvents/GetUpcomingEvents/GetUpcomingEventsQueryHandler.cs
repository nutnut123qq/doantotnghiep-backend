using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.CorporateEvents.GetUpcomingEvents;

public class GetUpcomingEventsQueryHandler : IRequestHandler<GetUpcomingEventsQuery, IEnumerable<CorporateEvent>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUpcomingEventsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CorporateEvent>> Handle(GetUpcomingEventsQuery request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.CorporateEvents.GetUpcomingEventsAsync(request.DaysAhead);
    }
}
