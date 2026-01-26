using MediatR;

namespace StockInvestment.Application.Features.Watchlist.UpdateWatchlist;

public class UpdateWatchlistCommand : IRequest<UpdateWatchlistResponse>
{
    public Guid WatchlistId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UpdateWatchlistResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
