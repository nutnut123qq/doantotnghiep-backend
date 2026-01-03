using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetAnalytics;

public class GetAnalyticsQueryHandler : IRequestHandler<GetAnalyticsQuery, ApiAnalytics>
{
    private readonly IAnalyticsService _analyticsService;

    public GetAnalyticsQueryHandler(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task<ApiAnalytics> Handle(GetAnalyticsQuery request, CancellationToken cancellationToken)
    {
        return await _analyticsService.GetApiAnalyticsAsync(request.StartDate, request.EndDate);
    }
}
