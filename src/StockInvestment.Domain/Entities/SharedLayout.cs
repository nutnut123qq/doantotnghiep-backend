namespace StockInvestment.Domain.Entities;

/// <summary>
/// Stores a shared layout snapshot by share code.
/// </summary>
public class SharedLayout
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public string LayoutJson { get; set; } = null!;
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Navigation properties
    public User Owner { get; set; } = null!;

    public SharedLayout()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}
