using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.AIModelConfig.GetAIModelConfig;

public class GetAIModelConfigQueryHandler : IRequestHandler<GetAIModelConfigQuery, AIModelConfigDto?>
{
    private readonly IAIModelConfigService _aiModelConfigService;
    private readonly ILogger<GetAIModelConfigQueryHandler> _logger;

    public GetAIModelConfigQueryHandler(
        IAIModelConfigService aiModelConfigService,
        ILogger<GetAIModelConfigQueryHandler> logger)
    {
        _aiModelConfigService = aiModelConfigService;
        _logger = logger;
    }

    public async Task<AIModelConfigDto?> Handle(GetAIModelConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _aiModelConfigService.GetConfigAsync(cancellationToken);
        
        if (config == null)
        {
            return null;
        }

        return new AIModelConfigDto
        {
            Id = config.Id,
            ModelName = config.ModelName,
            Version = config.Version,
            ApiKey = config.ApiKey, // Note: In production, mask this
            Settings = config.Settings,
            UpdateFrequencyMinutes = config.UpdateFrequencyMinutes,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
        };
    }
}

