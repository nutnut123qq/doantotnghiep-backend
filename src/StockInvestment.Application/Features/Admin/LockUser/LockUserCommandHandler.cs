using MediatR;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.LockUser;

public class LockUserCommandHandler : IRequestHandler<LockUserCommand, AdminActionResult>
{
    private readonly IAdminService _adminService;

    public LockUserCommandHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<AdminActionResult> Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        return await _adminService.LockUserAsync(request.AdminUserId, request.UserId);
    }
}
