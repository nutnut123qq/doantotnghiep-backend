using MediatR;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Watchlist.GetWatchlists;

public class GetWatchlistsQuery : IRequest<GetWatchlistsResponse>
{
    public Guid UserId { get; set; }
}

public class GetWatchlistsResponse
{
    public List<WatchlistDto> Watchlists { get; set; } = new();
}

public class WatchlistDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<StockTickerDto> Tickers { get; set; } = new();
}

public class StockTickerDto
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? Change { get; set; }
    public decimal? ChangePercent { get; set; }
    public long? Volume { get; set; }
}

