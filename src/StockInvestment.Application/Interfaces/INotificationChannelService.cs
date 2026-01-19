using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Interfaces;

public interface INotificationChannelService
{
    Task<NotificationChannelConfigDto?> GetUserConfigAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationChannelConfigDto> SaveConfigAsync(Guid userId, UpdateNotificationChannelRequest request, CancellationToken cancellationToken = default);
    Task SendAlertNotificationAsync(AlertTriggeredContext context, CancellationToken cancellationToken = default);
    Task<bool> TestChannelAsync(Guid userId, NotificationChannelType channelType, CancellationToken cancellationToken = default);
}
