using MediatR;

namespace StockInvestment.Application.Features.Watchlist.AddStockToWatchlist;

public class AddStockToWatchlistCommand : IRequest<AddStockToWatchlistResponse>
{
    public Guid WatchlistId { get; set; }
    public string Symbol { get; set; } = string.Empty;
}

public class AddStockToWatchlistResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

