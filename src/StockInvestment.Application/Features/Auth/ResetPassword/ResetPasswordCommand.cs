using MediatR;

namespace StockInvestment.Application.Features.Auth.ResetPassword;

public class ResetPasswordCommand : IRequest<ResetPasswordDto>
{
    public string Token { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;
}
