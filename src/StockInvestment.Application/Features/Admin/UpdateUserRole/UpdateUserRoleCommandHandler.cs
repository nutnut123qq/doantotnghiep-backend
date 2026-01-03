using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.UpdateUserRole;

public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, bool>
{
    private readonly IAdminService _adminService;

    public UpdateUserRoleCommandHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<bool> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        return await _adminService.UpdateUserRoleAsync(request.UserId, request.NewRole);
    }
}
