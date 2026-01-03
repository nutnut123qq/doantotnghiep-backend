namespace StockInvestment.Domain.Entities;

public class PushNotificationConfig
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = string.Empty; // "Firebase", "OneSignal", etc.
    public string? ServerKey { get; set; } // Encrypted
    public string? AppId { get; set; }
    public string? Config { get; set; } // JSON string for additional configuration
    public bool IsEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

