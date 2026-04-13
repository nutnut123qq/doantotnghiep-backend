using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AIModelConfigApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AIModelConfigApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetConfig_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/AIModelConfig/config")).StatusCode);

    [Fact]
    public async Task GetPerformance_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/AIModelConfig/performance")).StatusCode);

    [Fact]
    public async Task GetConfig_WithAuth_ReturnsOkOrForbidden()
        => Assert.True((await _factory.CreateAuthenticatedClient().GetAsync("api/AIModelConfig/config")).StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden);
}
