namespace StockInvestment.Application.Features.Auth.VerifyEmail;

public class VerifyEmailDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;

    public string? Token { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
}
