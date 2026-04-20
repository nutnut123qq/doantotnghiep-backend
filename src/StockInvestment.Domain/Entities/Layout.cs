namespace StockInvestment.Domain.Entities;

public class Layout
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string Configuration { get; set; } = null!; // JSON string for layout config
    public bool IsDefault { get; set; }
    public bool IsShared { get; set; }
    public string? ShareCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;

    public Layout()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsDefault = false;
        IsShared = false;
    }
}

