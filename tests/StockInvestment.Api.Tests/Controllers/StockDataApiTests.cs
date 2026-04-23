using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class StockDataApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StockDataApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetSymbols_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/StockData/symbols")).StatusCode);

    [Fact]
    public async Task GetQuote_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/StockData/quote/VNM")).StatusCode);

    [Fact]
    public async Task GetSymbols_WithAuth_ReturnsOk()
        => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateAuthenticatedClient().GetAsync("api/StockData/symbols")).StatusCode);

    [Fact]
    public async Task GetSymbols_WithAuth_ReturnsExactlyThirtyVn30Symbols()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/StockData/symbols");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(30, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetQuote_NonVn30Symbol_ReturnsNotFound()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/StockData/quote/ZZZZ");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetQuote_Vn30Symbol_ReturnsPayloadWithSymbol()
    {
        using (var scope = _factory.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.StockTickers.AnyAsync(t => t.Symbol == "VNM"))
            {
                db.StockTickers.Add(new StockTicker
                {
                    Symbol = "VNM",
                    Name = "Vinamilk",
                    Exchange = Exchange.HOSE,
                    CurrentPrice = 100000m,
                    LastUpdated = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/StockData/quote/VNM");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("VNM", doc.RootElement.GetProperty("symbol").GetString());
        Assert.True(doc.RootElement.TryGetProperty("currentPrice", out _));
    }

    [Fact]
    public async Task GetOHLCV_NonVn30Symbol_ReturnsNotFound()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/StockData/ohlcv/ZZZZ");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
