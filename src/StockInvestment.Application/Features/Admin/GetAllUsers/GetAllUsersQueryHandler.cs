using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, (IEnumerable<User> Users, int TotalCount)>
{
    private readonly IAdminService _adminService;

    public GetAllUsersQueryHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        return await _adminService.GetAllUsersAsync(request.Page, request.PageSize);
    }
}
