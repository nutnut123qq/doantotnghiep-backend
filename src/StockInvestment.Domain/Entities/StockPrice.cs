namespace StockInvestment.Domain.Entities;

/// <summary>
/// Daily OHLCV price data persisted to the database.
/// Replaces reliance on external AI service for historical price lookups.
/// </summary>
public class StockPrice
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public StockPrice()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
