using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Watchlist.AddStockToWatchlist;

public class AddStockToWatchlistHandler : IRequestHandler<AddStockToWatchlistCommand, AddStockToWatchlistResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVNStockService _vnStockService;
    private readonly ILogger<AddStockToWatchlistHandler> _logger;

    public AddStockToWatchlistHandler(
        IUnitOfWork unitOfWork,
        IVNStockService vnStockService,
        ILogger<AddStockToWatchlistHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _vnStockService = vnStockService;
        _logger = logger;
    }

    public async Task<AddStockToWatchlistResponse> Handle(AddStockToWatchlistCommand request, CancellationToken cancellationToken)
    {
        var watchlist = await _unitOfWork.Watchlists.GetByIdWithTickersAsync(request.WatchlistId, cancellationToken);

        if (watchlist == null)
        {
            return new AddStockToWatchlistResponse
            {
                Success = false,
                Message = "Watchlist not found"
            };
        }

        // Kiểm tra xem stock đã có trong watchlist chưa
        if (watchlist.Tickers.Any(t => t.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase)))
        {
            return new AddStockToWatchlistResponse
            {
                Success = false,
                Message = "Stock already in watchlist"
            };
        }

        // Tìm hoặc tạo stock ticker
        var ticker = await _unitOfWork.Repository<Domain.Entities.StockTicker>()
            .FirstOrDefaultAsync(t => t.Symbol == request.Symbol.ToUpper(), cancellationToken);

        if (ticker == null)
        {
            // Lấy thông tin từ VNStock
            ticker = await _vnStockService.GetQuoteAsync(request.Symbol);
            if (ticker == null)
            {
                return new AddStockToWatchlistResponse
                {
                    Success = false,
                    Message = "Stock not found"
                };
            }

            await _unitOfWork.Repository<Domain.Entities.StockTicker>().AddAsync(ticker, cancellationToken);
        }

        watchlist.Tickers.Add(ticker);
        watchlist.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added stock {Symbol} to watchlist {WatchlistId}", request.Symbol, request.WatchlistId);

        return new AddStockToWatchlistResponse
        {
            Success = true,
            Message = "Stock added successfully"
        };
    }
}

