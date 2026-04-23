using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AnalystContextApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AnalystContextApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateAnalystClient(bool authenticated = false)
    {
        var client = authenticated ? _factory.CreateAuthenticatedClient() : _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", "test-internal-key");
        return client;
    }

    [Fact]
    public async Task GetNewsContext_MissingSymbol_ReturnsBadRequest()
    {
        var response = await CreateAnalystClient(authenticated: true).GetAsync("api/rag/news-context?symbol=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTechSummary_WithSymbol_ReturnsOk()
    {
        var response = await CreateAnalystClient(authenticated: true).GetAsync("api/market/VNM/tech-summary?limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
