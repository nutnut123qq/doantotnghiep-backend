using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Watchlist.GetWatchlists;

public class GetWatchlistsHandler : IRequestHandler<GetWatchlistsQuery, GetWatchlistsResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetWatchlistsHandler> _logger;

    public GetWatchlistsHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetWatchlistsHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GetWatchlistsResponse> Handle(GetWatchlistsQuery request, CancellationToken cancellationToken)
    {
        var watchlists = await _unitOfWork.Watchlists.GetByUserIdWithTickersAsync(request.UserId, cancellationToken);

        var response = new GetWatchlistsResponse
        {
            Watchlists = watchlists.Select(w => new WatchlistDto
            {
                Id = w.Id,
                Name = w.Name,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
                Tickers = w.Tickers.Select(t => new StockTickerDto
                {
                    Id = t.Id,
                    Symbol = t.Symbol,
                    Name = t.Name,
                    Exchange = t.Exchange.ToString(),
                    Industry = t.Industry,
                    CurrentPrice = t.CurrentPrice,
                    Change = t.Change,
                    ChangePercent = t.ChangePercent,
                    Volume = t.Volume
                }).ToList()
            }).ToList()
        };

        return response;
    }
}

