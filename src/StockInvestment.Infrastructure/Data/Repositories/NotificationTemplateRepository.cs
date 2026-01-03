using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class NotificationTemplateRepository : Repository<NotificationTemplate>, INotificationTemplateRepository
{
    public NotificationTemplateRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<NotificationTemplate>> GetByEventTypeAsync(NotificationEventType eventType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.EventType == eventType)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationTemplate?> GetActiveTemplateAsync(NotificationEventType eventType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.EventType == eventType && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

