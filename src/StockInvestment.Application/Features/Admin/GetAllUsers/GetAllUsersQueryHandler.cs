using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Features.Admin.Models;

namespace StockInvestment.Application.Features.Admin.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, (IEnumerable<AdminUserDto> Users, int TotalCount)>
{
    private readonly IAdminService _adminService;

    public GetAllUsersQueryHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<(IEnumerable<AdminUserDto> Users, int TotalCount)> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _adminService.GetAllUsersAsync(request.Page, request.PageSize);
        var mappedUsers = users.Select(AdminUserDto.FromEntity);
        return (mappedUsers, totalCount);
    }
}
