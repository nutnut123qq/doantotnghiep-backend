using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetPopularStocks;

/// <summary>
/// Query to get popular stocks
/// </summary>
public class GetPopularStocksQuery : IRequest<List<PopularStock>>
{
    public int TopN { get; set; } = 10;
    public int DaysBack { get; set; } = 7;
}
