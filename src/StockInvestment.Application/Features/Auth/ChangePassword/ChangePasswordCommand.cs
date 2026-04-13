using MediatR;

namespace StockInvestment.Application.Features.Auth.ChangePassword;

public class ChangePasswordCommand : IRequest<ChangePasswordDto>
{
    public Guid UserId { get; set; }
    public string OldPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
    public string ConfirmNewPassword { get; set; } = null!;
}
