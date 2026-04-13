using System.Net;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AdminSystemApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminSystemApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("api/admin/health");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithNonAdmin_ReturnsForbidden()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/admin/stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithAdmin_ReturnsOk()
    {
        var response = await _factory.CreateAuthenticatedClient(role: "Admin").GetAsync("api/admin/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
