using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.UserPreferences.GetUserPreferences;

public class GetUserPreferencesQuery : IRequest<IEnumerable<UserPreference>>
{
    public Guid UserId { get; set; }

    public GetUserPreferencesQuery(Guid userId)
    {
        UserId = userId;
    }
}
