using MediatR;

namespace StockInvestment.Application.Features.UserPreferences.SaveUserPreference;

public class SaveUserPreferenceCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public string PreferenceKey { get; set; } = string.Empty;
    public string PreferenceValue { get; set; } = string.Empty;
}
