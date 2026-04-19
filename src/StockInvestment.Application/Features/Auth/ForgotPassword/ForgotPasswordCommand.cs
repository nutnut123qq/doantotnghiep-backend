using MediatR;

namespace StockInvestment.Application.Features.Auth.ForgotPassword;

public class ForgotPasswordCommand : IRequest<ForgotPasswordDto>
{
    public string Email { get; set; } = null!;
}
