using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
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
}
