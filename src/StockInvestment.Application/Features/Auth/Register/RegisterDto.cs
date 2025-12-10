namespace StockInvestment.Application.Features.Auth.Register;

public class RegisterDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string Message { get; set; } = null!;
}

