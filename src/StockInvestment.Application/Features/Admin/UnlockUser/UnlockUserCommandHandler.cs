using MediatR;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.UnlockUser;

public class UnlockUserCommandHandler : IRequestHandler<UnlockUserCommand, AdminActionResult>
{
    private readonly IAdminService _adminService;

    public UnlockUserCommandHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<AdminActionResult> Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        return await _adminService.UnlockUserAsync(request.AdminUserId, request.UserId);
    }
}
