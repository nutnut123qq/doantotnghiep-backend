using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Interfaces;

public interface INotificationTemplateRepository : IRepository<NotificationTemplate>
{
    Task<IEnumerable<NotificationTemplate>> GetByEventTypeAsync(NotificationEventType eventType, CancellationToken cancellationToken = default);
    Task<NotificationTemplate?> GetActiveTemplateAsync(NotificationEventType eventType, CancellationToken cancellationToken = default);
}

