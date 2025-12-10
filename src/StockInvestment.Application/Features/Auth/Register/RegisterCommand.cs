using MediatR;

namespace StockInvestment.Application.Features.Auth.Register;

public class RegisterCommand : IRequest<RegisterDto>
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;
}

