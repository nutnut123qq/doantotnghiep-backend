using MediatR;

namespace StockInvestment.Application.Features.Admin.PushNotification.Test;

public class TestPushNotificationCommand : IRequest<TestPushNotificationResponse>
{
    public string DeviceToken { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class TestPushNotificationResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

