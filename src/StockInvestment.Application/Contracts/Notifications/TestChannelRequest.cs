namespace StockInvestment.Application.Contracts.Notifications;

public class TestChannelRequest
{
    public string Channel { get; set; } = null!;  // "Slack" hoặc "Telegram"
}
