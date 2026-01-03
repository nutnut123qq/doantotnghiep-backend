using MediatR;

namespace StockInvestment.Application.Features.Admin.PushNotification.UpdateConfig;

public class UpdatePushNotificationConfigCommand : IRequest<PushNotificationConfigDto>
{
    public Guid? Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? ServerKey { get; set; }
    public string? AppId { get; set; }
    public string? Config { get; set; }
    public bool IsEnabled { get; set; }
}

public class PushNotificationConfigDto
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? ServerKey { get; set; }
    public string? AppId { get; set; }
    public string? Config { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

