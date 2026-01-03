namespace StockInvestment.Application.Interfaces;

public interface IPushNotificationService
{
    Task<bool> SendPushNotificationAsync(Guid userId, string title, string body, Dictionary<string, object>? data = null, CancellationToken cancellationToken = default);
    Task<bool> TestPushNotificationAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default);
}

