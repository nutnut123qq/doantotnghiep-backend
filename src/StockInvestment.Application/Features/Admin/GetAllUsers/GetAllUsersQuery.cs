using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.GetAllUsers;

/// <summary>
/// Query to get all users with pagination
/// </summary>
public class GetAllUsersQuery : IRequest<(IEnumerable<User> Users, int TotalCount)>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
