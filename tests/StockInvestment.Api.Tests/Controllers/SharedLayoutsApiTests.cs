using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class SharedLayoutsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SharedLayoutsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetSharedByCode_AllowAnonymous_ReturnsNotFoundWhenInvalid()
        => Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("api/layouts/shared/invalid-code-123")).StatusCode);

    [Fact]
    public async Task GetMyShared_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/layouts/shared")).StatusCode);

    [Fact]
    public async Task GetMyShared_WithAuth_ReturnsOk()
        => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateAuthenticatedClient().GetAsync("api/layouts/shared")).StatusCode);
}
