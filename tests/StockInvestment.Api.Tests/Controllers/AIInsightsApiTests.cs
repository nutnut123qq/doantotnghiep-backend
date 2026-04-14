using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;
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
    public async Task GetInsights_WithIncludeDeletedAndNonAdmin_ReturnsForbidden()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/AIInsights?includeDeleted=true");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetDeletedStatus_WithNonAdminUser_ReturnsForbidden()
    {
        var response = await _factory
            .CreateAuthenticatedClient()
            .PatchAsJsonAsync($"api/AIInsights/{Guid.NewGuid()}/deleted", new { isDeleted = true });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetDeletedStatus_WithAdminUserAndUnknownInsight_ReturnsNotFound()
    {
        var response = await _factory
            .CreateAuthenticatedClient(role: "Admin")
            .PatchAsJsonAsync($"api/AIInsights/{Guid.NewGuid()}/deleted", new { isDeleted = true });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetDeletedStatus_WithAdminUser_HidesInsightSuccessfully()
    {
        var insightId = await SeedAiInsightAsync(isDeleted: false);
        var client = _factory.CreateAuthenticatedClient(role: "Admin");

        var response = await client.PatchAsJsonAsync($"api/AIInsights/{insightId}/deleted", new { isDeleted = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.AIInsights.FindAsync(insightId);
        Assert.NotNull(saved);
        Assert.True(saved!.IsDeleted);
    }

    [Fact]
    public async Task SetDeletedStatus_WithAdminUser_UnhidesInsightSuccessfully()
    {
        var insightId = await SeedAiInsightAsync(isDeleted: true);
        var client = _factory.CreateAuthenticatedClient(role: "Admin");

        var response = await client.PatchAsJsonAsync($"api/AIInsights/{insightId}/deleted", new { isDeleted = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.AIInsights.FindAsync(insightId);
        Assert.NotNull(saved);
        Assert.False(saved!.IsDeleted);
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

    private async Task<Guid> SeedAiInsightAsync(bool isDeleted)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var ticker = new StockTicker
        {
            Id = Guid.NewGuid(),
            Symbol = $"T{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Name = "Test Ticker",
            CurrentPrice = 100_000m,
            Exchange = Exchange.HOSE,
            LastUpdated = DateTime.UtcNow
        };
        db.StockTickers.Add(ticker);

        var insight = new AIInsight
        {
            Id = Guid.NewGuid(),
            TickerId = ticker.Id,
            Ticker = ticker,
            Type = InsightType.Buy,
            Title = "Test insight",
            Description = "Test description",
            Confidence = 80,
            Reasoning = "[\"Reason 1\"]",
            GeneratedAt = DateTime.UtcNow,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AIInsights.Add(insight);

        await db.SaveChangesAsync();
        return insight.Id;
    }
}
