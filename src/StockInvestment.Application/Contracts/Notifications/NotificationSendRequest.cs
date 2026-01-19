using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Contracts.Notifications;

public class NotificationSendRequest
{
    public NotificationChannelType ChannelType { get; set; }
    public string Destination { get; set; } = null!;  // Webhook URL hoặc ChatId
    public string Message { get; set; } = null!;
    public string? Subject { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
