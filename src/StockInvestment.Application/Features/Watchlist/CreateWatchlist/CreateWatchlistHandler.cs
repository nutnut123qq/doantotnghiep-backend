using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Watchlist.CreateWatchlist;

public class CreateWatchlistHandler : IRequestHandler<CreateWatchlistCommand, CreateWatchlistResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateWatchlistHandler> _logger;

    public CreateWatchlistHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateWatchlistHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateWatchlistResponse> Handle(CreateWatchlistCommand request, CancellationToken cancellationToken)
    {
        var watchlist = new Domain.Entities.Watchlist
        {
            UserId = request.UserId,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Watchlists.AddAsync(watchlist, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created watchlist {WatchlistId} for user {UserId}", watchlist.Id, request.UserId);

        return new CreateWatchlistResponse
        {
            Id = watchlist.Id,
            Name = watchlist.Name,
            CreatedAt = watchlist.CreatedAt
        };
    }
}

