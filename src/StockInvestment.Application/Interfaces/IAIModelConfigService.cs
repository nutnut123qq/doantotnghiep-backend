using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IAIModelConfigService
{
    Task<AIModelConfig?> GetConfigAsync(CancellationToken cancellationToken = default);
    Task<AIModelConfig> UpdateConfigAsync(AIModelConfig config, CancellationToken cancellationToken = default);
    Task<IEnumerable<AIModelPerformance>> GetPerformanceMetricsAsync(DateTime? startDate = null, CancellationToken cancellationToken = default);
    Task RecordPerformanceAsync(AIModelPerformance performance, CancellationToken cancellationToken = default);
}

