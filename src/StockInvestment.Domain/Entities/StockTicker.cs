using StockInvestment.Domain.Enums;

namespace StockInvestment.Domain.Entities;

public class StockTicker
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Exchange Exchange { get; set; }
    public string? Industry { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? PreviousClose { get; set; }
    public decimal? Change { get; set; }
    public decimal? ChangePercent { get; set; }
    public long? Volume { get; set; }
    public decimal? Value { get; set; }
    public DateTime LastUpdated { get; set; }

    // Navigation properties
    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<News> News { get; set; } = new List<News>();
    public ICollection<FinancialReport> FinancialReports { get; set; } = new List<FinancialReport>();
    public ICollection<TechnicalIndicator> TechnicalIndicators { get; set; } = new List<TechnicalIndicator>();
    public ICollection<CorporateEvent> CorporateEvents { get; set; } = new List<CorporateEvent>();

    public StockTicker()
    {
        Id = Guid.NewGuid();
        LastUpdated = DateTime.UtcNow;
    }
}

