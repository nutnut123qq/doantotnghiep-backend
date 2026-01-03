using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.AIModelConfig.GetAIModelConfig;

public class GetAIModelConfigQuery : IRequest<AIModelConfigDto?>
{
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

