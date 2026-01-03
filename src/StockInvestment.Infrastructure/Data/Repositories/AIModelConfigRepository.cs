using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class AIModelConfigRepository : Repository<AIModelConfig>, IAIModelConfigRepository
{
    public AIModelConfigRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<AIModelConfig?> GetActiveConfigAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<AIModelPerformance>> GetPerformanceMetricsAsync(Guid modelConfigId, DateTime? startDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<AIModelPerformance>()
            .Where(p => p.ModelConfigId == modelConfigId);

        if (startDate.HasValue)
        {
            query = query.Where(p => p.RecordedAt >= startDate.Value);
        }

        return await query
            .OrderByDescending(p => p.RecordedAt)
            .ToListAsync(cancellationToken);
    }
}

