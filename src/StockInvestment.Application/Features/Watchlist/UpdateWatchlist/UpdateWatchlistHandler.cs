using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;

namespace StockInvestment.Application.Features.Watchlist.UpdateWatchlist;

public class UpdateWatchlistHandler : IRequestHandler<UpdateWatchlistCommand, UpdateWatchlistResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateWatchlistHandler> _logger;

    public UpdateWatchlistHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateWatchlistHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UpdateWatchlistResponse> Handle(UpdateWatchlistCommand request, CancellationToken cancellationToken)
    {
        var watchlist = await _unitOfWork.Watchlists.GetByIdAsync(request.WatchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", request.WatchlistId);
        }

        // Verify ownership
        if (watchlist.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to update this watchlist");
        }

        watchlist.Name = request.Name;
        watchlist.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Watchlists.UpdateAsync(watchlist);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated watchlist {WatchlistId} for user {UserId}", watchlist.Id, request.UserId);

        return new UpdateWatchlistResponse
        {
            Id = watchlist.Id,
            Name = watchlist.Name,
            CreatedAt = watchlist.CreatedAt,
            UpdatedAt = watchlist.UpdatedAt
        };
    }
}
