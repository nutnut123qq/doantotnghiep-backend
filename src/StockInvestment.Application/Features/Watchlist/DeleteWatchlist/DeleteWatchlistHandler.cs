using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;

namespace StockInvestment.Application.Features.Watchlist.DeleteWatchlist;

public class DeleteWatchlistHandler : IRequestHandler<DeleteWatchlistCommand, DeleteWatchlistResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteWatchlistHandler> _logger;

    public DeleteWatchlistHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeleteWatchlistHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DeleteWatchlistResponse> Handle(DeleteWatchlistCommand request, CancellationToken cancellationToken)
    {
        var watchlist = await _unitOfWork.Watchlists.GetByIdAsync(request.WatchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", request.WatchlistId);
        }

        // Verify ownership
        if (watchlist.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this watchlist");
        }

        await _unitOfWork.Watchlists.DeleteAsync(watchlist);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted watchlist {WatchlistId} for user {UserId}", watchlist.Id, request.UserId);

        return new DeleteWatchlistResponse
        {
            Success = true,
            Message = "Watchlist deleted successfully"
        };
    }
}
