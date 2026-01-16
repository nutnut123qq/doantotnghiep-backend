namespace StockInvestment.Domain.Entities;

/// <summary>
/// Email verification token for account activation
/// </summary>
public class EmailVerificationToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;

    public EmailVerificationToken()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsUsed = false;
        // Token expires in 24 hours
        ExpiresAt = DateTime.UtcNow.AddHours(24);
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired;
}
