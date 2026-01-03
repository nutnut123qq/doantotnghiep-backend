using MediatR;

namespace StockInvestment.Application.Features.Admin.SetUserActiveStatus;

/// <summary>
/// Command to activate/deactivate user
/// </summary>
public class SetUserActiveStatusCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
}
