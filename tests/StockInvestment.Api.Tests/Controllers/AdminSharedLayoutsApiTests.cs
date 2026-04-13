using System.Net;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AdminSharedLayoutsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminSharedLayoutsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetSharedLayouts_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("api/admin/shared-layouts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedLayouts_WithNonAdmin_ReturnsForbidden()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/admin/shared-layouts");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSharedLayouts_WithAdminInvalidOwnerId_ReturnsBadRequest()
    {
        var response = await _factory.CreateAuthenticatedClient(role: "Admin")
            .GetAsync("api/admin/shared-layouts?ownerId=not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
