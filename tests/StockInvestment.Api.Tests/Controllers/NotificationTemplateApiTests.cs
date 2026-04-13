using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class NotificationTemplateApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationTemplateApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetTemplates_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/NotificationTemplate")).StatusCode);

    [Fact]
    public async Task GetTemplates_WithAuth_ReturnsOkOrForbidden()
        => Assert.True((await _factory.CreateAuthenticatedClient().GetAsync("api/NotificationTemplate")).StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden);
}
