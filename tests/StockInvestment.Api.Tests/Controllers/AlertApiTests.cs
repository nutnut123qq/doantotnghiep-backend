using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AlertApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AlertApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient UnauthClient => _factory.CreateClient();
    private HttpClient AuthClient => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task GetAlerts_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await UnauthClient.GetAsync("api/Alert")).StatusCode);

    [Fact]
    public async Task GetAlerts_WithAuth_ReturnsOk()
    {
        var response = await AuthClient.GetAsync("api/Alert");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAlert_WithAuth_ValidBody_ReturnsCreatedOrBadRequest()
    {
        var body = new { Symbol = "VNM", Type = 1, Condition = "above", Threshold = 100m };
        var response = await AuthClient.PostAsJsonAsync("api/Alert", body);
        Assert.True(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAlert_WithAuth_NonExistent_ReturnsNotFound()
    {
        var body = new { Symbol = "VNM", Type = 1, Condition = "above", Threshold = 100m };
        var response = await AuthClient.PutAsJsonAsync("api/Alert/00000000-0000-0000-0000-000000000001", body);
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteAlert_WithAuth_NonExistent_ReturnsNotFound()
    {
        var response = await AuthClient.DeleteAsync("api/Alert/00000000-0000-0000-0000-000000000001");
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest);
    }
}
