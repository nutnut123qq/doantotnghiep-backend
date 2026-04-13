using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class WatchlistApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WatchlistApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient UnauthClient => _factory.CreateClient();
    private HttpClient AuthClient => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task GetWatchlists_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await UnauthClient.GetAsync("api/Watchlist")).StatusCode);

    [Fact]
    public async Task GetWatchlists_WithAuth_ReturnsOk()
    {
        var response = await AuthClient.GetAsync("api/Watchlist");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateWatchlist_WithAuth_ReturnsCreated()
    {
        var response = await AuthClient.PostAsJsonAsync("api/Watchlist", new { Name = "My List " + Guid.NewGuid().ToString("N")[..8] });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWatchlist_WithAuth_NonExistentId_ReturnsNotFound()
    {
        var response = await AuthClient.PutAsJsonAsync("api/Watchlist/00000000-0000-0000-0000-000000000001", new { Name = "Updated" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWatchlist_WithAuth_NonExistentId_ReturnsNotFound()
    {
        var response = await AuthClient.DeleteAsync("api/Watchlist/00000000-0000-0000-0000-000000000001");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
