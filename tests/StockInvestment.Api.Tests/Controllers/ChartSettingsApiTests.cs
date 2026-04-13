using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class ChartSettingsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ChartSettingsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetSettings_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/ChartSettings")).StatusCode);

    [Fact]
    public async Task GetSettings_WithAuth_ReturnsOk()
        => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateAuthenticatedClient().GetAsync("api/ChartSettings")).StatusCode);
}
