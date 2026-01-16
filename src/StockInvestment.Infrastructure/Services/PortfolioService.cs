using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class PortfolioService : IPortfolioService
{
    private readonly ILogger<PortfolioService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IStockDataService _stockDataService;
    private readonly ICacheService _cacheService;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;

    public PortfolioService(
        ILogger<PortfolioService> logger,
        IUnitOfWork unitOfWork,
        IPortfolioRepository portfolioRepository,
        IStockDataService stockDataService,
        ICacheService cacheService,
        ICacheKeyGenerator cacheKeyGenerator)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _portfolioRepository = portfolioRepository;
        _stockDataService = stockDataService;
        _cacheService = cacheService;
        _cacheKeyGenerator = cacheKeyGenerator;
    }

    public async Task<IEnumerable<PortfolioHoldingDto>> GetHoldingsAsync(Guid userId)
    {
        try
        {
            // Check cache first
            var cacheKey = _cacheKeyGenerator.GeneratePortfolioHoldingsKey(userId);
            var cachedHoldings = await _cacheService.GetAsync<List<PortfolioHoldingDto>>(cacheKey);
            if (cachedHoldings != null)
            {
                return cachedHoldings;
            }

            // Load all portfolios for user
            var portfolios = await _portfolioRepository.GetByUserIdAsync(userId);
            var portfoliosList = portfolios.ToList();

            if (!portfoliosList.Any())
            {
                return new List<PortfolioHoldingDto>();
            }

            // Batch load all tickers in one query (fix N+1 problem)
            var symbols = portfoliosList.Select(p => p.Symbol).Distinct().ToList();
            var tickers = await _stockDataService.GetTickersBySymbolsAsync(symbols);

            var holdings = new List<PortfolioHoldingDto>();

            foreach (var portfolio in portfolios)
            {
                // Get ticker from batch-loaded dictionary
                tickers.TryGetValue(portfolio.Symbol, out var ticker);
                
                var currentPrice = ticker?.CurrentPrice ?? portfolio.CurrentPrice;
                var name = ticker?.Name ?? portfolio.Name;

                // Calculate values
                var value = portfolio.Shares * currentPrice;
                var totalCost = portfolio.Shares * portfolio.AvgPrice;
                var gainLoss = value - totalCost;
                var gainLossPercentage = totalCost > 0 ? (gainLoss / totalCost) * 100 : 0;

                holdings.Add(new PortfolioHoldingDto
                {
                    Id = portfolio.Id.ToString(),
                    Symbol = portfolio.Symbol,
                    Name = name,
                    Shares = portfolio.Shares,
                    AvgPrice = portfolio.AvgPrice,
                    CurrentPrice = currentPrice,
                    Value = value,
                    GainLoss = gainLoss,
                    GainLossPercentage = gainLossPercentage
                });
            }

            // Cache for 2 minutes
            await _cacheService.SetAsync(cacheKey, holdings, TimeSpan.FromMinutes(2));

            return holdings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching holdings for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PortfolioSummaryDto> GetSummaryAsync(Guid userId)
    {
        try
        {
            // Check cache first
            var cacheKey = _cacheKeyGenerator.GeneratePortfolioSummaryKey(userId);
            var cachedSummary = await _cacheService.GetAsync<PortfolioSummaryDto>(cacheKey);
            if (cachedSummary != null)
            {
                return cachedSummary;
            }

            // Calculate summary directly from database with aggregation (optimize - no need to load all holdings)
            var portfolios = await _portfolioRepository.GetByUserIdAsync(userId);
            var portfoliosList = portfolios.ToList();

            if (!portfoliosList.Any())
            {
                var emptySummary = new PortfolioSummaryDto
                {
                    TotalValue = 0,
                    TotalCost = 0,
                    TotalGainLoss = 0,
                    TotalGainLossPercentage = 0,
                    HoldingsCount = 0
                };
                await _cacheService.SetAsync(cacheKey, emptySummary, TimeSpan.FromMinutes(2));
                return emptySummary;
            }

            // Batch load tickers for current prices
            var symbols = portfoliosList.Select(p => p.Symbol).Distinct().ToList();
            var tickers = await _stockDataService.GetTickersBySymbolsAsync(symbols);

            // Calculate summary using aggregation
            decimal totalValue = 0;
            decimal totalCost = 0;
            int holdingsCount = portfoliosList.Count;

            foreach (var portfolio in portfoliosList)
            {
                tickers.TryGetValue(portfolio.Symbol, out var ticker);
                var currentPrice = ticker?.CurrentPrice ?? portfolio.CurrentPrice;
                
                totalValue += portfolio.Shares * currentPrice;
                totalCost += portfolio.Shares * portfolio.AvgPrice;
            }

            var totalGainLoss = totalValue - totalCost;
            var totalGainLossPercentage = totalCost > 0 ? (totalGainLoss / totalCost) * 100 : 0;

            var summary = new PortfolioSummaryDto
            {
                TotalValue = totalValue,
                TotalCost = totalCost,
                TotalGainLoss = totalGainLoss,
                TotalGainLossPercentage = totalGainLossPercentage,
                HoldingsCount = holdingsCount
            };

            // Cache for 2 minutes
            await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(2));

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating summary for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PortfolioHoldingDto> AddHoldingAsync(Guid userId, AddHoldingRequest request)
    {
        try
        {
            // Validate symbol exists
            var ticker = await _stockDataService.GetTickerBySymbolAsync(request.Symbol);
            if (ticker == null)
            {
                throw new Domain.Exceptions.ValidationException("Symbol", $"Stock symbol '{request.Symbol}' not found");
            }

            // Check if holding already exists for this user and symbol
            var existingHolding = await _portfolioRepository.FindByUserAndSymbolAsync(userId, request.Symbol);

            if (existingHolding != null)
            {
                // Update existing holding by combining shares and recalculating avg price
                var totalShares = existingHolding.Shares + request.Shares;
                var totalCost = (existingHolding.Shares * existingHolding.AvgPrice) + (request.Shares * request.AvgPrice);
                var newAvgPrice = totalCost / totalShares;

                existingHolding.Shares = totalShares;
                existingHolding.AvgPrice = newAvgPrice;
                existingHolding.CurrentPrice = ticker.CurrentPrice;
                existingHolding.Name = ticker.Name;
                existingHolding.UpdatedAt = DateTime.UtcNow;

                // Recalculate
                var value = existingHolding.Shares * existingHolding.CurrentPrice;
                var cost = existingHolding.Shares * existingHolding.AvgPrice;
                existingHolding.Value = value;
                existingHolding.GainLoss = value - cost;
                existingHolding.GainLossPercentage = cost > 0 ? (existingHolding.GainLoss / cost) * 100 : 0;

                await _portfolioRepository.UpdateAsync(existingHolding);
                await _portfolioRepository.SaveChangesAsync();

                // Invalidate cache
                await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioHoldingsKey(userId));
                await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioSummaryKey(userId));

                return new PortfolioHoldingDto
                {
                    Id = existingHolding.Id.ToString(),
                    Symbol = existingHolding.Symbol,
                    Name = existingHolding.Name,
                    Shares = existingHolding.Shares,
                    AvgPrice = existingHolding.AvgPrice,
                    CurrentPrice = existingHolding.CurrentPrice,
                    Value = existingHolding.Value,
                    GainLoss = existingHolding.GainLoss,
                    GainLossPercentage = existingHolding.GainLossPercentage
                };
            }

            // Create new holding
            var portfolio = new Portfolio
            {
                UserId = userId,
                Symbol = request.Symbol,
                Name = ticker.Name,
                Shares = request.Shares,
                AvgPrice = request.AvgPrice,
                CurrentPrice = ticker.CurrentPrice
            };

            // Calculate initial values
            portfolio.Value = portfolio.Shares * portfolio.CurrentPrice;
            var portfolioTotalCost = portfolio.Shares * portfolio.AvgPrice;
            portfolio.GainLoss = portfolio.Value - portfolioTotalCost;
            portfolio.GainLossPercentage = portfolioTotalCost > 0 ? (portfolio.GainLoss / portfolioTotalCost) * 100 : 0;

            await _portfolioRepository.AddAsync(portfolio);
            await _portfolioRepository.SaveChangesAsync();

            // Invalidate cache
            await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioHoldingsKey(userId));
            await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioSummaryKey(userId));

            return new PortfolioHoldingDto
            {
                Id = portfolio.Id.ToString(),
                Symbol = portfolio.Symbol,
                Name = portfolio.Name,
                Shares = portfolio.Shares,
                AvgPrice = portfolio.AvgPrice,
                CurrentPrice = portfolio.CurrentPrice,
                Value = portfolio.Value,
                GainLoss = portfolio.GainLoss,
                GainLossPercentage = portfolio.GainLossPercentage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding holding for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PortfolioHoldingDto> UpdateHoldingAsync(Guid userId, Guid holdingId, UpdateHoldingRequest request)
    {
        try
        {
            var portfolio = await _portfolioRepository.GetByIdAndUserIdAsync(holdingId, userId);

            if (portfolio == null)
            {
                throw new Domain.Exceptions.NotFoundException("Portfolio", holdingId);
            }

            // Update fields
            portfolio.Shares = request.Shares;
            portfolio.AvgPrice = request.AvgPrice;
            portfolio.UpdatedAt = DateTime.UtcNow;

            // Get latest price and name
            var ticker = await _stockDataService.GetTickerBySymbolAsync(portfolio.Symbol);
            if (ticker != null)
            {
                portfolio.CurrentPrice = ticker.CurrentPrice;
                portfolio.Name = ticker.Name;
            }

            // Recalculate
            portfolio.Value = portfolio.Shares * portfolio.CurrentPrice;
            var totalCost = portfolio.Shares * portfolio.AvgPrice;
            portfolio.GainLoss = portfolio.Value - totalCost;
            portfolio.GainLossPercentage = totalCost > 0 ? (portfolio.GainLoss / totalCost) * 100 : 0;

            await _portfolioRepository.UpdateAsync(portfolio);
            await _portfolioRepository.SaveChangesAsync();

            // Invalidate cache
            await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioHoldingsKey(userId));
            await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioSummaryKey(userId));

            return new PortfolioHoldingDto
            {
                Id = portfolio.Id.ToString(),
                Symbol = portfolio.Symbol,
                Name = portfolio.Name,
                Shares = portfolio.Shares,
                AvgPrice = portfolio.AvgPrice,
                CurrentPrice = portfolio.CurrentPrice,
                Value = portfolio.Value,
                GainLoss = portfolio.GainLoss,
                GainLossPercentage = portfolio.GainLossPercentage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating holding {HoldingId} for user {UserId}", holdingId, userId);
            throw;
        }
    }

    public async Task DeleteHoldingAsync(Guid userId, Guid holdingId)
    {
        try
        {
            var portfolio = await _portfolioRepository.GetByIdAndUserIdAsync(holdingId, userId);

            if (portfolio == null)
            {
                throw new Domain.Exceptions.NotFoundException("Portfolio", holdingId);
            }

            await _portfolioRepository.DeleteAsync(portfolio);
            await _portfolioRepository.SaveChangesAsync();

            // Invalidate cache
            await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioHoldingsKey(userId));
            await _cacheService.RemoveAsync(_cacheKeyGenerator.GeneratePortfolioSummaryKey(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting holding {HoldingId} for user {UserId}", holdingId, userId);
            throw;
        }
    }
}
