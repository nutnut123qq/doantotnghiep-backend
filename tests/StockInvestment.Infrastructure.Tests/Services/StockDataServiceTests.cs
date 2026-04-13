using Microsoft.Extensions.Logging;
using Moq;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Services;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Services;

public class StockDataServiceTests
{
    private readonly Mock<ILogger<StockDataService>> _logger = new();
    private readonly Mock<IVNStockService> _vnStockService = new();
    private readonly Mock<IStockTickerRepository> _stockTickerRepository = new();
    private readonly Mock<IWatchlistRepository> _watchlistRepository = new();
    private readonly Mock<ICacheService> _cacheService = new();

    [Fact]
    public async Task GetTickersAsync_DbHasOnlyStaleData_ReturnsDbDataWithoutCallingVnStock()
    {
        var staleTicker = new StockTicker
        {
            Symbol = "ACB",
            Name = "ACB",
            Exchange = Exchange.HOSE,
            CurrentPrice = 25000m,
            LastUpdated = DateTime.UtcNow.AddHours(-2)
        };

        _cacheService
            .Setup(x => x.GetAsync<List<StockTicker>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<StockTicker>?)null);

        _stockTickerRepository
            .Setup(x => x.GetTickersAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockTicker> { staleTicker });

        var sut = CreateSut();

        var result = (await sut.GetTickersAsync()).ToList();

        Assert.Single(result);
        Assert.Equal("ACB", result[0].Symbol);
        _vnStockService.Verify(x => x.GetQuotesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GetTickersAsync_NoCacheNoDb_ReturnsEmptyWithoutCallingVnStock()
    {
        _cacheService
            .Setup(x => x.GetAsync<List<StockTicker>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<StockTicker>?)null);

        _stockTickerRepository
            .Setup(x => x.GetTickersAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockTicker>());

        var sut = CreateSut();

        var result = (await sut.GetTickersAsync()).ToList();

        Assert.Empty(result);
        _vnStockService.Verify(x => x.GetQuotesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GetTickersAsync_UnsupportedIndex_ReturnsEmpty()
    {
        _cacheService
            .Setup(x => x.GetAsync<List<StockTicker>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<StockTicker>?)null);
        _stockTickerRepository
            .Setup(x => x.GetTickersAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockTicker>());

        var sut = CreateSut();
        var result = await sut.GetTickersAsync(index: "VN100");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTickerBySymbolAsync_NonVn30_UsesRepositoryOnly()
    {
        var ticker = new StockTicker { Symbol = "AAA", Name = "AAA", Exchange = Exchange.HOSE };
        _stockTickerRepository
            .Setup(x => x.GetBySymbolAsync("AAA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticker);

        var sut = CreateSut();
        var result = await sut.GetTickerBySymbolAsync(" aaa ");

        Assert.NotNull(result);
        Assert.Equal("AAA", result!.Symbol);
        _vnStockService.Verify(x => x.GetQuoteAsync(It.IsAny<string>()), Times.Never);
    }

    private StockDataService CreateSut()
    {
        return new StockDataService(
            _logger.Object,
            _vnStockService.Object,
            _stockTickerRepository.Object,
            _watchlistRepository.Object,
            _cacheService.Object);
    }
}
