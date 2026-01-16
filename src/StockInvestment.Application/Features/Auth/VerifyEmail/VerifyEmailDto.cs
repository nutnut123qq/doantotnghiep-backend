namespace StockInvestment.Application.Features.Auth.VerifyEmail;

public class VerifyEmailDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}
