using MediatR;
using StockInvestment.Application.Features.Admin.Models;

namespace StockInvestment.Application.Features.Admin.LockUser;

public class LockUserCommand : IRequest<AdminActionResult>
{
    public Guid AdminUserId { get; set; }
    public Guid UserId { get; set; }
}
