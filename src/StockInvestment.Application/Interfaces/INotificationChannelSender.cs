using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Interfaces;

public interface INotificationChannelSender
{
    NotificationChannelType ChannelType { get; }
    Task<bool> SendAsync(NotificationSendRequest request, CancellationToken cancellationToken = default);
}
