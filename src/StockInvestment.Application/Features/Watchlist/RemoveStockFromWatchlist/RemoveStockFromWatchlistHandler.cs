using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Watchlist.RemoveStockFromWatchlist;

public class RemoveStockFromWatchlistHandler : IRequestHandler<RemoveStockFromWatchlistCommand, RemoveStockFromWatchlistResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveStockFromWatchlistHandler> _logger;

    public RemoveStockFromWatchlistHandler(
        IUnitOfWork unitOfWork,
        ILogger<RemoveStockFromWatchlistHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RemoveStockFromWatchlistResponse> Handle(RemoveStockFromWatchlistCommand request, CancellationToken cancellationToken)
    {
        var watchlist = await _unitOfWork.Watchlists.GetByIdWithTickersAsync(request.WatchlistId, cancellationToken);

        if (watchlist == null)
        {
            return new RemoveStockFromWatchlistResponse
            {
                Success = false,
                Message = "Watchlist not found"
            };
        }

        var ticker = watchlist.Tickers.FirstOrDefault(t => t.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase));
        if (ticker == null)
        {
            return new RemoveStockFromWatchlistResponse
            {
                Success = false,
                Message = "Stock not found in watchlist"
            };
        }

        watchlist.Tickers.Remove(ticker);
        watchlist.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Removed stock {Symbol} from watchlist {WatchlistId}", request.Symbol, request.WatchlistId);

        return new RemoveStockFromWatchlistResponse
        {
            Success = true,
            Message = "Stock removed successfully"
        };
    }
}

