using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetEndpointMetrics;

/// <summary>
/// Query to get endpoint performance metrics
/// </summary>
public class GetEndpointMetricsQuery : IRequest<List<EndpointMetrics>>
{
    public int TopN { get; set; } = 20;
}
