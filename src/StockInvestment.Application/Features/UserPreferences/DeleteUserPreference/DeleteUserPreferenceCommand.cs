using MediatR;

namespace StockInvestment.Application.Features.UserPreferences.DeleteUserPreference;

public class DeleteUserPreferenceCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public string PreferenceKey { get; set; } = string.Empty;
}
