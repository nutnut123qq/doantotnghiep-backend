using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class WorkspaceMessagesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkspaceMessagesApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMessages_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync($"api/workspace/{Guid.NewGuid()}/messages");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsBadRequest()
    {
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync($"api/workspace/{Guid.NewGuid()}/messages", new { content = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
