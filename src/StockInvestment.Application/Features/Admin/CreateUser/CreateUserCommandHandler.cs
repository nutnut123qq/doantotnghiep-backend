using MediatR;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, AdminActionResult<AdminUserDto>>
{
    private readonly IAdminService _adminService;

    public CreateUserCommandHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<AdminActionResult<AdminUserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        return await _adminService.CreateUserAsync(
            request.AdminUserId,
            request.Email,
            request.Password,
            request.Role,
            request.FullName);
    }
}
