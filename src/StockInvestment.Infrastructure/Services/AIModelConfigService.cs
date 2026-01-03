using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class AIModelConfigService : IAIModelConfigService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIModelConfigRepository _configRepository;
    private readonly ILogger<AIModelConfigService> _logger;

    public AIModelConfigService(
        IUnitOfWork unitOfWork,
        IAIModelConfigRepository configRepository,
        ILogger<AIModelConfigService> logger)
    {
        _unitOfWork = unitOfWork;
        _configRepository = configRepository;
        _logger = logger;
    }

    public async Task<AIModelConfig?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        return await _configRepository.GetActiveConfigAsync(cancellationToken);
    }

    public async Task<AIModelConfig> UpdateConfigAsync(AIModelConfig config, CancellationToken cancellationToken = default)
    {
        config.UpdatedAt = DateTime.UtcNow;

        var existing = await _configRepository.GetByIdAsync(config.Id, cancellationToken);

        if (existing == null)
        {
            config.Id = Guid.NewGuid();
            config.CreatedAt = DateTime.UtcNow;
            await _configRepository.AddAsync(config, cancellationToken);
        }
        else
        {
            await _configRepository.UpdateAsync(config, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated AI model config: {ModelName} ({Id})", config.ModelName, config.Id);

        return config;
    }

    public async Task<IEnumerable<AIModelPerformance>> GetPerformanceMetricsAsync(DateTime? startDate = null, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken);
        if (config == null)
        {
            return Enumerable.Empty<AIModelPerformance>();
        }

        return await _configRepository.GetPerformanceMetricsAsync(config.Id, startDate, cancellationToken);
    }

    public async Task RecordPerformanceAsync(AIModelPerformance performance, CancellationToken cancellationToken = default)
    {
        performance.Id = Guid.NewGuid();
        performance.RecordedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<AIModelPerformance>().AddAsync(performance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

