using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class PortfolioApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PortfolioApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHoldings_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/Portfolio/holdings")).StatusCode);

    [Fact]
    public async Task GetSummary_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/Portfolio/summary")).StatusCode);

    [Fact]
    public async Task GetHoldings_WithAuth_ReturnsOk()
        => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateAuthenticatedClient().GetAsync("api/Portfolio/holdings")).StatusCode);
}
