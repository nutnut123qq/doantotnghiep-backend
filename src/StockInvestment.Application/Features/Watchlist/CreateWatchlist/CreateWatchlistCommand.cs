using MediatR;

namespace StockInvestment.Application.Features.Watchlist.CreateWatchlist;

public class CreateWatchlistCommand : IRequest<CreateWatchlistResponse>
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CreateWatchlistResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

