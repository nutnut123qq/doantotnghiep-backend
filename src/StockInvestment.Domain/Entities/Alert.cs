using StockInvestment.Domain.Enums;

namespace StockInvestment.Domain.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? TickerId { get; set; }
    public AlertType Type { get; set; }
    public string Condition { get; set; } = null!; // JSON string for complex conditions
    public decimal? Threshold { get; set; }
    public string? Timeframe { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public StockTicker? Ticker { get; set; }

    public Alert()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }
}

