using MediatR;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.CreateUser;

public class CreateUserCommand : IRequest<AdminActionResult<AdminUserDto>>
{
    public Guid AdminUserId { get; set; }
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public UserRole Role { get; set; }
    public string? FullName { get; set; }
}
