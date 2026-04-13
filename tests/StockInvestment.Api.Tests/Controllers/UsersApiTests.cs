using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class UsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UsersApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetUsers_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/Users")).StatusCode);

    [Fact]
    public async Task GetUsers_WithInvestor_ReturnsForbidden()
        => Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateAuthenticatedClient().GetAsync("api/Users")).StatusCode);

    [Fact]
    public async Task GetUsers_WithAdmin_ReturnsOk()
    {
        var response = await _factory.CreateAuthenticatedClient(role: "Admin").GetAsync("api/Users");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
