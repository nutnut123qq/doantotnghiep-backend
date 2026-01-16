using MediatR;

namespace StockInvestment.Application.Features.Auth.VerifyEmail;

public class VerifyEmailCommand : IRequest<VerifyEmailDto>
{
    public string Token { get; set; } = null!;
}
