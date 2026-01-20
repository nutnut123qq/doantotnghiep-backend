using MediatR;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, AdminActionResult>
{
    private readonly IAdminService _adminService;

    public UpdateUserCommandHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<AdminActionResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        return await _adminService.UpdateUserAsync(
            request.AdminUserId,
            request.UserId,
            request.Email,
            request.FullName,
            request.Role);
    }
}
