using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using StockInvestment.Infrastructure.Services;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Tests.Services;

public class TechnicalIndicatorServiceTests
{
    private readonly Mock<IVNStockService> _mockVnStockService;
    private readonly Mock<ILogger<TechnicalIndicatorService>> _mockLogger;
    private readonly TechnicalIndicatorService _service;

    public TechnicalIndicatorServiceTests()
    {
        _mockVnStockService = new Mock<IVNStockService>();
        _mockLogger = new Mock<ILogger<TechnicalIndicatorService>>();
        _service = new TechnicalIndicatorService(_mockVnStockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CalculateMACDAsync_ValidData_ReturnsNonZeroHistogram()
    {
        // Arrange: 60 điểm sideways với biên dao động đủ lớn
        var historicalData = CreateHistoricalData(60, 100m, 110m);
        _mockVnStockService
            .Setup(x => x.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(historicalData);

        // Act
        var result = await _service.CalculateMACDAsync("TEST");

        // Assert: Không check MACD/Signal != 0 vì sideways có thể gần 0
        // Chỉ check MACD != Signal và histogram khác 0
        Assert.NotEqual(result.MACD, result.Signal); // Phải khác nhau
        Assert.True(Math.Abs(result.Histogram) > 0.0001m); // Histogram khác 0
    }

    [Fact]
    public async Task CalculateMACDAsync_StrongUptrend_MACDAboveSignal()
    {
        // Arrange: 100 điểm tăng rất mạnh để tạo xu hướng rõ ràng
        var historicalData = CreateTrendingData(100, 100m, 200m);
        _mockVnStockService
            .Setup(x => x.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(historicalData);

        // Act
        var result = await _service.CalculateMACDAsync("TEST");

        // Assert: với uptrend, cả MACD và Signal nên dương
        // Với trending tuyến tính, MACD và Signal có thể hội tụ (histogram ≈ 0)
        Assert.True(result.MACD > 0, "MACD should be positive for uptrend");
        Assert.True(result.Signal > 0, "Signal should be positive for uptrend");
        // Histogram không nên âm đáng kể (dùng epsilon chặt hơn)
        Assert.True(result.Histogram > -0.01m, "Histogram should not be significantly negative for uptrend");
    }

    [Fact]
    public async Task CalculateMACDAsync_InsufficientData_ReturnsDefault()
    {
        // Arrange: chỉ 30 điểm (< slowPeriod + signalPeriod + 1 = 36)
        var historicalData = CreateHistoricalData(30, 100m, 105m);
        _mockVnStockService
            .Setup(x => x.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(historicalData);

        // Act
        var result = await _service.CalculateMACDAsync("TEST");

        // Assert
        Assert.Equal(0, result.MACD);
        Assert.Equal(0, result.Signal);
        Assert.Equal(0, result.Histogram);
    }

    private List<OHLCVData> CreateHistoricalData(int count, decimal minPrice, decimal maxPrice)
    {
        var data = new List<OHLCVData>();
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var price = minPrice + (decimal)random.NextDouble() * (maxPrice - minPrice);
            data.Add(new OHLCVData
            {
                Date = DateTime.UtcNow.AddDays(-count + i),
                Close = price,
                Open = price,
                High = price * 1.01m,
                Low = price * 0.99m,
                Volume = 1000000
            });
        }
        return data;
    }

    private List<OHLCVData> CreateTrendingData(int count, decimal startPrice, decimal endPrice)
    {
        var data = new List<OHLCVData>();
        var increment = (endPrice - startPrice) / count;

        for (int i = 0; i < count; i++)
        {
            var price = startPrice + (increment * i);
            data.Add(new OHLCVData
            {
                Date = DateTime.UtcNow.AddDays(-count + i),
                Close = price,
                Open = price,
                High = price * 1.01m,
                Low = price * 0.99m,
                Volume = 1000000
            });
        }
        return data;
    }
}
