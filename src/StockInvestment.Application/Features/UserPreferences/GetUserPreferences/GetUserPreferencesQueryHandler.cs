using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.UserPreferences.GetUserPreferences;

public class GetUserPreferencesQueryHandler : IRequestHandler<GetUserPreferencesQuery, IEnumerable<UserPreference>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserPreferencesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<UserPreference>> Handle(GetUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.UserPreferences.GetByUserIdAsync(request.UserId);
    }
}
