using StockInvestment.Domain.Enums;

namespace StockInvestment.Domain.Entities;

public class DataSource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DataSourceType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; } = true;
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Unknown;
    public DateTime? LastChecked { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Config { get; set; } // JSON string for additional configuration
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

