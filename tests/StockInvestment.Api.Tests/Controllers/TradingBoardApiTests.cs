using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class TradingBoardApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TradingBoardApiTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetTickers_NoAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("api/TradingBoard");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTickerById_NonExistent_ReturnsNotFound()
        => Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("api/TradingBoard/00000000-0000-0000-0000-000000000001")).StatusCode);

    [Fact]
    public async Task GetTickers_InvalidIndex_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("api/TradingBoard?index=VN100");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
