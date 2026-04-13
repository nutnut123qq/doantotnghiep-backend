using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class WorkspaceApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkspaceApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient UnauthClient => _factory.CreateClient();
    private HttpClient AuthClient => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task GetWorkspaces_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await UnauthClient.GetAsync("api/Workspace")).StatusCode);

    [Fact]
    public async Task GetWorkspaces_WithAuth_ReturnsOk()
    {
        var response = await AuthClient.GetAsync("api/Workspace");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorkspace_WithAuth_ReturnsCreated()
    {
        var response = await AuthClient.PostAsJsonAsync("api/Workspace", new { Name = "Test WS " + Guid.NewGuid().ToString("N")[..8], Description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateWorkspace_WithAuth_EmptyName_ReturnsBadRequest()
    {
        var response = await AuthClient.PostAsJsonAsync("api/Workspace", new { Name = "", Description = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkspace_WithAuth_NonExistent_ReturnsNotFound()
    {
        var response = await AuthClient.PutAsJsonAsync("api/Workspace/00000000-0000-0000-0000-000000000001", new { Name = "Updated", Description = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkspace_WithAuth_NonExistent_ReturnsNotFound()
    {
        var response = await AuthClient.DeleteAsync("api/Workspace/00000000-0000-0000-0000-000000000001");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetWorkspace_WithAuth_ReturnsConsistentPayload()
    {
        var createResponse = await AuthClient.PostAsJsonAsync("api/Workspace", new
        {
            Name = "Payload WS " + Guid.NewGuid().ToString("N")[..8],
            Description = "payload-test"
        });
        createResponse.EnsureSuccessStatusCode();

        var createdBody = await createResponse.Content.ReadAsStringAsync();
        using var createdJson = JsonDocument.Parse(createdBody);
        var workspaceId = createdJson.RootElement.GetProperty("id").GetGuid();

        var getResponse = await AuthClient.GetAsync($"api/Workspace/{workspaceId}");
        getResponse.EnsureSuccessStatusCode();
        var getBody = await getResponse.Content.ReadAsStringAsync();
        using var getJson = JsonDocument.Parse(getBody);
        Assert.Equal(workspaceId, getJson.RootElement.GetProperty("id").GetGuid());
    }
}
