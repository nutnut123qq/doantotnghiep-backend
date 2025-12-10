namespace StockInvestment.Application.Features.Auth.Login;

public class LoginDto
{
    public string Token { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
}

