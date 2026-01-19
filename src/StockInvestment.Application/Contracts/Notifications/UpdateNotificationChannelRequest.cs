namespace StockInvestment.Application.Contracts.Notifications;

public class UpdateNotificationChannelRequest
{
    public string? SlackWebhookUrl { get; set; }  // Null = không update
    public bool EnabledSlack { get; set; }
    public string? TelegramChatId { get; set; }  // Null = không update
    public bool EnabledTelegram { get; set; }
}
