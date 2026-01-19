namespace StockInvestment.Infrastructure.Configuration;

public class NotificationChannelOptions
{
    public TelegramOptions Telegram { get; set; } = new();
}

public class TelegramOptions
{
    public string BotToken { get; set; } = string.Empty;
}
