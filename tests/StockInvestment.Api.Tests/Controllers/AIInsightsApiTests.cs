using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AIInsightsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AIInsightsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetInsights_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/AIInsights")).StatusCode);

    [Fact]
    public async Task GetSentiment_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/AIInsights/sentiment")).StatusCode);

    [Fact]
    public async Task GetInsights_WithAuth_ReturnsOk()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/AIInsights");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task DismissInsight_WithNonAdminUser_ReturnsForbidden()
    {
        var response = await _factory.CreateAuthenticatedClient().PostAsync($"api/AIInsights/{Guid.NewGuid()}/dismiss", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GenerateInsight_WithAdminAndMissingSymbol_ReturnsBadRequestWithError()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var response = await client.PostAsJsonAsync("api/AIInsights/generate", new { symbol = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task GetAccuracyMetrics_WithAuth_ReturnsOk()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/AIInsights/metrics/accuracy?maxInsights=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
