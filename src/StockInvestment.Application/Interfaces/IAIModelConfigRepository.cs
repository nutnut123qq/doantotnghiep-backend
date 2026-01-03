using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IAIModelConfigRepository : IRepository<AIModelConfig>
{
    Task<AIModelConfig?> GetActiveConfigAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<AIModelPerformance>> GetPerformanceMetricsAsync(Guid modelConfigId, DateTime? startDate = null, CancellationToken cancellationToken = default);
}

