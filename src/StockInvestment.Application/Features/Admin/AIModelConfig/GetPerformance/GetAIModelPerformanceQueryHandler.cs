using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.AIModelConfig.GetPerformance;

public class GetAIModelPerformanceQueryHandler : IRequestHandler<GetAIModelPerformanceQuery, GetAIModelPerformanceResponse>
{
    private readonly IAIModelConfigService _aiModelConfigService;
    private readonly ILogger<GetAIModelPerformanceQueryHandler> _logger;

    public GetAIModelPerformanceQueryHandler(
        IAIModelConfigService aiModelConfigService,
        ILogger<GetAIModelPerformanceQueryHandler> logger)
    {
        _aiModelConfigService = aiModelConfigService;
        _logger = logger;
    }

    public async Task<GetAIModelPerformanceResponse> Handle(GetAIModelPerformanceQuery request, CancellationToken cancellationToken)
    {
        var metrics = await _aiModelConfigService.GetPerformanceMetricsAsync(request.StartDate, cancellationToken);
        
        var dtos = metrics.Select(m => new AIModelPerformanceDto
        {
            Id = m.Id,
            FeatureType = m.FeatureType,
            Accuracy = m.Accuracy,
            AverageResponseTimeMs = m.AverageResponseTimeMs,
            SuccessCount = m.SuccessCount,
            FailureCount = m.FailureCount,
            RecordedAt = m.RecordedAt,
        }).ToList();

        var summary = new PerformanceSummary
        {
            OverallAccuracy = dtos.Any() ? dtos.Average(d => d.Accuracy) : 0,
            OverallAverageResponseTimeMs = dtos.Any() ? dtos.Average(d => d.AverageResponseTimeMs) : 0,
            TotalSuccessCount = dtos.Sum(d => d.SuccessCount),
            TotalFailureCount = dtos.Sum(d => d.FailureCount),
        };

        var totalRequests = summary.TotalSuccessCount + summary.TotalFailureCount;
        summary.SuccessRate = totalRequests > 0
            ? (summary.TotalSuccessCount / (double)totalRequests) * 100
            : 0;

        return new GetAIModelPerformanceResponse
        {
            Metrics = dtos,
            Summary = summary,
        };
    }
}

