using MediatR;
using StockInvestment.Application.Features.Admin.Models;

namespace StockInvestment.Application.Features.Admin.ResetUserPassword;

public class ResetUserPasswordCommand : IRequest<AdminActionResult>
{
    public Guid AdminUserId { get; set; }
    public Guid UserId { get; set; }
    public string NewPassword { get; set; } = null!;
}
