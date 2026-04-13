using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AdminApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetUsers_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/Admin/users")).StatusCode);

    [Fact]
    public async Task GetStats_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/Admin/stats")).StatusCode);

    [Fact]
    public async Task GetUsers_WithAdminAuth_ReturnsOk()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var response = await client.GetAsync("api/Admin/users");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithAdminAuth_ReturnsOk()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var response = await client.GetAsync("api/Admin/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
