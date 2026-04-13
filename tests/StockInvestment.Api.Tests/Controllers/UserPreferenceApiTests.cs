using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class UserPreferenceApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserPreferenceApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetPreferences_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/UserPreference")).StatusCode);

    [Fact]
    public async Task GetPreferences_WithAuth_ReturnsOk()
        => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateAuthenticatedClient().GetAsync("api/UserPreference")).StatusCode);
}
