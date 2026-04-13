using System.Net;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AnalystContextApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AnalystContextApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetNewsContext_MissingSymbol_ReturnsBadRequest()
    {
        var response = await _factory.CreateClient().GetAsync("api/rag/news-context?symbol=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTechSummary_WithSymbol_ReturnsOk()
    {
        var response = await _factory.CreateClient().GetAsync("api/market/VNM/tech-summary?limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
