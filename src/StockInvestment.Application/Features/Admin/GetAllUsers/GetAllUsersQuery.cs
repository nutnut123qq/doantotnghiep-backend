using MediatR;
using StockInvestment.Application.Features.Admin.Models;

namespace StockInvestment.Application.Features.Admin.GetAllUsers;

/// <summary>
/// Query to get all users with pagination
/// </summary>
public class GetAllUsersQuery : IRequest<(IEnumerable<AdminUserDto> Users, int TotalCount)>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
