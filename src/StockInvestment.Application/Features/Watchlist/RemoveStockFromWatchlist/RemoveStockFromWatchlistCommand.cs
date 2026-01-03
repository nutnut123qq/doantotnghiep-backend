using MediatR;

namespace StockInvestment.Application.Features.Watchlist.RemoveStockFromWatchlist;

public class RemoveStockFromWatchlistCommand : IRequest<RemoveStockFromWatchlistResponse>
{
    public Guid WatchlistId { get; set; }
    public string Symbol { get; set; } = string.Empty;
}

public class RemoveStockFromWatchlistResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

