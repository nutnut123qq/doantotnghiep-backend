using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.SetUserActiveStatus;

public class SetUserActiveStatusCommandHandler : IRequestHandler<SetUserActiveStatusCommand, bool>
{
    private readonly IAdminService _adminService;

    public SetUserActiveStatusCommandHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<bool> Handle(SetUserActiveStatusCommand request, CancellationToken cancellationToken)
    {
        return await _adminService.SetUserActiveStatusAsync(request.UserId, request.IsActive);
    }
}
