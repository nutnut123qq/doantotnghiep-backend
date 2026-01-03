using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetEndpointMetrics;

public class GetEndpointMetricsQueryHandler : IRequestHandler<GetEndpointMetricsQuery, List<EndpointMetrics>>
{
    private readonly IAnalyticsService _analyticsService;

    public GetEndpointMetricsQueryHandler(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task<List<EndpointMetrics>> Handle(GetEndpointMetricsQuery request, CancellationToken cancellationToken)
    {
        return await _analyticsService.GetEndpointMetricsAsync(request.TopN);
    }
}
