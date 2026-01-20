using MediatR;
using StockInvestment.Application.Features.Admin.Models;

namespace StockInvestment.Application.Features.Admin.UnlockUser;

public class UnlockUserCommand : IRequest<AdminActionResult>
{
    public Guid AdminUserId { get; set; }
    public Guid UserId { get; set; }
}
