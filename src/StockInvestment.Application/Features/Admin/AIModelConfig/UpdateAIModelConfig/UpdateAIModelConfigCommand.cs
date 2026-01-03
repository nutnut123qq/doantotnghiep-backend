using MediatR;

namespace StockInvestment.Application.Features.Admin.AIModelConfig.UpdateAIModelConfig;

public class UpdateAIModelConfigCommand : IRequest<AIModelConfigDto>
{
    public Guid? Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Settings { get; set; }
    public int UpdateFrequencyMinutes { get; set; }
    public bool IsActive { get; set; }
}

public class AIModelConfigDto
{
    public Guid Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Settings { get; set; }
    public int UpdateFrequencyMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

