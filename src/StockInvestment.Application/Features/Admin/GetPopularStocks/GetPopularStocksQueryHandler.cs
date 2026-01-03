using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetPopularStocks;

public class GetPopularStocksQueryHandler : IRequestHandler<GetPopularStocksQuery, List<PopularStock>>
{
    private readonly IAnalyticsService _analyticsService;

    public GetPopularStocksQueryHandler(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task<List<PopularStock>> Handle(GetPopularStocksQuery request, CancellationToken cancellationToken)
    {
        return await _analyticsService.GetPopularStocksAsync(request.TopN, request.DaysBack);
    }
}
