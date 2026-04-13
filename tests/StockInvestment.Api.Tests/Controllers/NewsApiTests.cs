using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class NewsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public NewsApiTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetNews_NoAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("api/News");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetNewsById_NonExistent_ReturnsNotFound()
        => Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("api/News/00000000-0000-0000-0000-000000000001")).StatusCode);

    [Fact]
    public async Task Summarize_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync("api/News/00000000-0000-0000-0000-000000000001/summarize", null)).StatusCode);
}
