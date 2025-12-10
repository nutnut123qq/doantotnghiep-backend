namespace StockInvestment.Domain.Entities;

public class TechnicalIndicator
{
    public Guid Id { get; set; }
    public Guid TickerId { get; set; }
    public string IndicatorType { get; set; } = null!; // MA, RSI, MACD, etc.
    public decimal? Value { get; set; }
    public string? TrendAssessment { get; set; } // Bullish, Bearish, Neutral
    public DateTime CalculatedAt { get; set; }

    // Navigation properties
    public StockTicker Ticker { get; set; } = null!;

    public TechnicalIndicator()
    {
        Id = Guid.NewGuid();
        CalculatedAt = DateTime.UtcNow;
    }
}

