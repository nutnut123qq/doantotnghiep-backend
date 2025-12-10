using StockInvestment.Domain.Enums;

namespace StockInvestment.Domain.Entities;

public class News
{
    public Guid Id { get; set; }
    public Guid? TickerId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string? Url { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Summary { get; set; }
    public Sentiment? Sentiment { get; set; }
    public string? ImpactAssessment { get; set; }

    // Navigation properties
    public StockTicker? Ticker { get; set; }

    public News()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}

