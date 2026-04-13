namespace StockInvestment.Application.Contracts.Notifications;

public class NotificationChannelConfigDto
{
    public bool HasSlackWebhook { get; set; }
    public string? SlackWebhookMasked { get; set; }  // Masked value for edit UX
    public bool EnabledSlack { get; set; }
    public string? TelegramChatId { get; set; }
    public bool EnabledTelegram { get; set; }
}
