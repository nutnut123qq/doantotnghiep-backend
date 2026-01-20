namespace StockInvestment.Domain.Entities;

/// <summary>
/// Password reset token for admin reset operations
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;

    public PasswordResetToken()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsUsed = false;
        ExpiresAt = DateTime.UtcNow.AddMinutes(30);
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired;
}
