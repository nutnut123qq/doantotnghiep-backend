using MediatR;

namespace StockInvestment.Application.Features.Admin.AIModelConfig.GetPerformance;

public class GetAIModelPerformanceQuery : IRequest<GetAIModelPerformanceResponse>
{
    public DateTime? StartDate { get; set; }
}

public class GetAIModelPerformanceResponse
{
    public List<AIModelPerformanceDto> Metrics { get; set; } = new();
    public PerformanceSummary Summary { get; set; } = new();
}

public class AIModelPerformanceDto
{
    public Guid Id { get; set; }
    public string FeatureType { get; set; } = string.Empty;
    public double Accuracy { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class PerformanceSummary
{
    public double OverallAccuracy { get; set; }
    public double OverallAverageResponseTimeMs { get; set; }
    public int TotalSuccessCount { get; set; }
    public int TotalFailureCount { get; set; }
    public double SuccessRate { get; set; }
}

