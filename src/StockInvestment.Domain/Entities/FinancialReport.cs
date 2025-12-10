namespace StockInvestment.Domain.Entities;

public class FinancialReport
{
    public Guid Id { get; set; }
    public Guid TickerId { get; set; }
    public string ReportType { get; set; } = null!; // Quarterly, Annual, etc.
    public int Year { get; set; }
    public int? Quarter { get; set; }
    public string Content { get; set; } = null!; // JSON or structured data
    public DateTime ReportDate { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public StockTicker Ticker { get; set; } = null!;

    public FinancialReport()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}

