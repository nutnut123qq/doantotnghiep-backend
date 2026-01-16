using StockInvestment.Domain.Enums;

namespace StockInvestment.Domain.Entities;

public class AIInsight
{
    public Guid Id { get; set; }
    public Guid TickerId { get; set; }
    public InsightType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int Confidence { get; set; } // 0-100
    public string Reasoning { get; set; } = null!; // JSON array of strings
    public decimal? TargetPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public Guid? DismissedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public StockTicker Ticker { get; set; } = null!;
    public User? DismissedByUser { get; set; }

    public AIInsight()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        GeneratedAt = DateTime.UtcNow;
    }
}
