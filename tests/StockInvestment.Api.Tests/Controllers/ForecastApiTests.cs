using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class ForecastApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ForecastApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetForecast_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/Forecast/VNM")).StatusCode);

    [Fact]
    public async Task GetForecast_WithAuth_ReturnsOk()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/Forecast/VNM?timeHorizon=medium");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("VNM", json.RootElement.GetProperty("symbol").GetString());
        Assert.Equal("medium", json.RootElement.GetProperty("time_horizon").GetString());
    }

    [Fact]
    public async Task GetForecast_QuotaExceeded_Returns429WithErrorShape()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/Forecast/ERR429");
        Assert.Equal((HttpStatusCode)429, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Quota exceeded", json.RootElement.GetProperty("error").GetString());
        Assert.Equal("ERR429", json.RootElement.GetProperty("symbol").GetString());
    }

    [Fact]
    public async Task GetForecast_InternalAiError_Returns500WithErrorShape()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/Forecast/ERR500");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("AI service error", json.RootElement.GetProperty("error").GetString());
        Assert.Equal("ERR500", json.RootElement.GetProperty("symbol").GetString());
    }

    [Fact]
    public async Task GetBatchForecasts_WithAuth_ReturnsItemsForAllSymbols()
    {
        var payload = new { symbols = new[] { "VNM", "FPT" }, timeHorizon = "short" };
        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("api/Forecast/batch", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("forecasts");
        Assert.Equal(2, items.GetArrayLength());
    }

    [Fact]
    public async Task GetBatchForecasts_WithoutAuth_ReturnsUnauthorized()
    {
        var payload = new { symbols = new[] { "VNM" }, timeHorizon = "short" };
        var response = await _factory.CreateClient().PostAsJsonAsync("api/Forecast/batch", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
