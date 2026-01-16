namespace StockInvestment.Application.Features.Auth.ResendVerification;

public class ResendVerificationDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}
