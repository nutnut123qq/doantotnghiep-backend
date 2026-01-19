namespace StockInvestment.Domain.Entities;

public class NotificationChannelConfig
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }  // 1 row per user, unique index
    
    // Slack - per user
    public string? SlackWebhookUrl { get; set; }
    public bool EnabledSlack { get; set; }
    
    // Telegram - per user (bot token global trong appsettings)
    public string? TelegramChatId { get; set; }  // String: group chat id có thể âm/dài
    public bool EnabledTelegram { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Concurrency control
    public byte[] RowVersion { get; set; } = null!;
    
    // Navigation
    public User User { get; set; } = null!;

    public NotificationChannelConfig()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
