using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.AIModelConfig.UpdateAIModelConfig;

public class UpdateAIModelConfigCommandHandler : IRequestHandler<UpdateAIModelConfigCommand, AIModelConfigDto>
{
    private readonly IAIModelConfigService _aiModelConfigService;
    private readonly ILogger<UpdateAIModelConfigCommandHandler> _logger;

    public UpdateAIModelConfigCommandHandler(
        IAIModelConfigService aiModelConfigService,
        ILogger<UpdateAIModelConfigCommandHandler> logger)
    {
        _aiModelConfigService = aiModelConfigService;
        _logger = logger;
    }

    public async Task<AIModelConfigDto> Handle(UpdateAIModelConfigCommand request, CancellationToken cancellationToken)
    {
        var existing = request.Id.HasValue
            ? await _aiModelConfigService.GetConfigAsync(cancellationToken)
            : null;

        var config = existing ?? new Domain.Entities.AIModelConfig
        {
            Id = request.Id ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        config.ModelName = request.ModelName;
        config.Version = request.Version;
        config.ApiKey = request.ApiKey;
        config.Settings = request.Settings;
        config.UpdateFrequencyMinutes = request.UpdateFrequencyMinutes;
        config.IsActive = request.IsActive;

        var updated = await _aiModelConfigService.UpdateConfigAsync(config, cancellationToken);

        return new AIModelConfigDto
        {
            Id = updated.Id,
            ModelName = updated.ModelName,
            Version = updated.Version,
            ApiKey = updated.ApiKey,
            Settings = updated.Settings,
            UpdateFrequencyMinutes = updated.UpdateFrequencyMinutes,
            IsActive = updated.IsActive,
            CreatedAt = updated.CreatedAt,
            UpdatedAt = updated.UpdatedAt,
        };
    }
}

