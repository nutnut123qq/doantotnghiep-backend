using MediatR;

namespace StockInvestment.Application.Features.Auth.ResendVerification;

public class ResendVerificationCommand : IRequest<ResendVerificationDto>
{
    public string Email { get; set; } = null!;
}
