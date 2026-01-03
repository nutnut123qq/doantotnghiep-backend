using MediatR;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.UpdateUserRole;

/// <summary>
/// Command to update user role
/// </summary>
public class UpdateUserRoleCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public UserRole NewRole { get; set; }
}
