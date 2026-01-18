namespace StockInvestment.Domain.Entities;

public class ChartSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Symbol { get; set; } = null!;
    public string TimeRange { get; set; } = "3M"; // 1M, 3M, 6M, 1Y, ALL
    public string ChartType { get; set; } = "candlestick"; // candlestick, line, area
    public string Indicators { get; set; } = "[]"; // JSON: ["MA20", "MA50", "RSI"]
    public string Drawings { get; set; } = "{}"; // JSON: { trendlines: [], zones: [] }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;

    public ChartSettings()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
