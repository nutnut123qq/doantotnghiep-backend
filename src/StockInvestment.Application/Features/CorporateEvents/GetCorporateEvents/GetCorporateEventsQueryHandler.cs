using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.CorporateEvents.GetCorporateEvents;

public class GetCorporateEventsQueryHandler : IRequestHandler<GetCorporateEventsQuery, IEnumerable<CorporateEvent>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCorporateEventsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CorporateEvent>> Handle(GetCorporateEventsQuery request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.CorporateEvents.GetAllAsync(
            symbol: request.Symbol,
            eventType: request.EventType,
            startDate: request.StartDate,
            endDate: request.EndDate,
            status: request.Status);
    }
}
