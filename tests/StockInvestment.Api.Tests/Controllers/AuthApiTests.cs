using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockInvestment.Application.Features.Auth.Register;
using StockInvestment.Api.Tests.Helpers;
using Xunit;
using System.Text.Json;

namespace StockInvestment.Api.Tests.Controllers;

public class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient Client => _factory.CreateClient();

    [Fact]
    public async Task Register_ValidPayload_ReturnsOk()
    {
        var command = new RegisterCommand
        {
            Email = $"test-{Guid.NewGuid():N}@example.com",
            Password = "Test123!@#",
            ConfirmPassword = "Test123!@#"
        };

        var response = await Client.PostAsJsonAsync("api/auth/register", command);
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<RegisterDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.UserId);
        Assert.Equal(command.Email, dto.Email);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var command = new RegisterCommand
        {
            Email = "not-an-email",
            Password = "Test123!@#",
            ConfirmPassword = "Test123!@#"
        };

        var response = await Client.PostAsJsonAsync("api/auth/register", command);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_PasswordsMismatch_ReturnsBadRequest()
    {
        var command = new RegisterCommand
        {
            Email = "test@example.com",
            Password = "Test123!@#",
            ConfirmPassword = "Other456!@#"
        };

        var response = await Client.PostAsJsonAsync("api/auth/register", command);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsStandardErrorShape()
    {
        var command = new RegisterCommand
        {
            Email = "x",
            Password = "Test123!@#",
            ConfirmPassword = "Test123!@#"
        };

        var response = await Client.PostAsJsonAsync("api/auth/register", command);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Login_WithSeededTestUser_ReturnsOkAndToken()
    {
        _ = _factory.CreateAuthenticatedClient(); // ensure test user is seeded
        var loginBody = new { Email = TestUserConstants.TestUserEmail, Password = TestUserConstants.TestUserPassword };
        var response = await Client.PostAsJsonAsync("api/auth/login", loginBody);
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsNonSuccess()
    {
        _ = _factory.CreateAuthenticatedClient();
        var loginBody = new { Email = TestUserConstants.TestUserEmail, Password = "wrong-password" };
        var response = await Client.PostAsJsonAsync("api/auth/login", loginBody);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task VerifyEmail_MissingToken_ReturnsBadRequest()
    {
        var response = await Client.PostAsync("api/auth/verify-email?token=", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
