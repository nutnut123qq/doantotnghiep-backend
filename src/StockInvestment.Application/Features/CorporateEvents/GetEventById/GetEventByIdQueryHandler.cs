using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.CorporateEvents.GetEventById;

public class GetEventByIdQueryHandler : IRequestHandler<GetEventByIdQuery, CorporateEvent?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetEventByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CorporateEvent?> Handle(GetEventByIdQuery request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.CorporateEvents.GetByIdAsync(request.Id);
    }
}
