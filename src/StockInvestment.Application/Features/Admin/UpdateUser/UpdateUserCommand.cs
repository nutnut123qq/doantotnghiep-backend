using MediatR;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.UpdateUser;

public class UpdateUserCommand : IRequest<AdminActionResult>
{
    public Guid AdminUserId { get; set; }
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public UserRole? Role { get; set; }
}
