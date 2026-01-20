using MediatR;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.ResetUserPassword;

public class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand, AdminActionResult>
{
    private readonly IAdminService _adminService;

    public ResetUserPasswordCommandHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<AdminActionResult> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        return await _adminService.ResetPasswordAsync(request.AdminUserId, request.UserId, request.NewPassword);
    }
}
