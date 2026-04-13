using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class TechnicalIndicatorApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TechnicalIndicatorApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetIndicators_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/TechnicalIndicator/VNM")).StatusCode);

    [Fact]
    public async Task GetIndicator_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/TechnicalIndicator/VNM/RSI?period=14")).StatusCode);

    [Fact]
    public async Task GetIndicators_WithAuth_ReturnsOk()
        => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateAuthenticatedClient().GetAsync("api/TechnicalIndicator/VNM")).StatusCode);

    [Fact]
    public async Task GetIndicators_WithAuth_DefaultLiveFalse_ReturnsJsonArray()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/TechnicalIndicator/VIC");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("indicators", out var arr));
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
    }

    [Fact]
    public async Task GetIndicator_Rsi_WithoutStoredData_ReturnsNotFound()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/TechnicalIndicator/VIC/RSI?period=14");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIndicators_WhenDbSeeded_ReturnsStoredIndicatorCount()
    {
        using (var scope = _factory.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ticker = await db.StockTickers.FirstOrDefaultAsync(t => t.Symbol == "VNM");
            if (ticker == null)
            {
                ticker = new StockTicker
                {
                    Symbol = "VNM",
                    Name = "Vinamilk",
                    Exchange = Exchange.HOSE,
                    CurrentPrice = 100000m,
                    LastUpdated = DateTime.UtcNow
                };
                db.StockTickers.Add(ticker);
                await db.SaveChangesAsync();
            }

            db.TechnicalIndicators.RemoveRange(db.TechnicalIndicators.Where(i => i.TickerId == ticker.Id));
            await db.SaveChangesAsync();

            foreach (var type in new[] { "MA20", "MA50", "RSI", "MACD" })
            {
                db.TechnicalIndicators.Add(new TechnicalIndicator
                {
                    TickerId = ticker.Id,
                    IndicatorType = type,
                    Value = 42m,
                    CalculatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }

        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/TechnicalIndicator/VNM");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(4, doc.RootElement.GetProperty("indicators").GetArrayLength());
    }

    [Fact]
    public async Task GetIndicators_LiveTrue_ReturnsOk()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/TechnicalIndicator/VNM?live=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
