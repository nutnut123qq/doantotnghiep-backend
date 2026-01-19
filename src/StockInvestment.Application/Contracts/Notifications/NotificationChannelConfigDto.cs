namespace StockInvestment.Application.Contracts.Notifications;

public class NotificationChannelConfigDto
{
    public bool HasSlackWebhook { get; set; }  // Chỉ trả flag, không trả URL
    public bool EnabledSlack { get; set; }
    public string? TelegramChatId { get; set; }
    public bool EnabledTelegram { get; set; }
}
