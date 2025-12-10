using MediatR;

namespace StockInvestment.Application.Features.Auth.Login;

public class LoginCommand : IRequest<LoginDto>
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

