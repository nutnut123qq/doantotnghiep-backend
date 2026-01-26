using MediatR;

namespace StockInvestment.Application.Features.Watchlist.DeleteWatchlist;

public class DeleteWatchlistCommand : IRequest<DeleteWatchlistResponse>
{
    public Guid WatchlistId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteWatchlistResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
