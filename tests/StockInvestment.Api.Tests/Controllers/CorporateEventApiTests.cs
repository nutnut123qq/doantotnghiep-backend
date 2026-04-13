using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class CorporateEventApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CorporateEventApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEvents_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/CorporateEvent")).StatusCode);

    [Fact]
    public async Task GetUpcoming_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/CorporateEvent/upcoming")).StatusCode);

    [Fact]
    public async Task GetEvents_WithAuth_ReturnsOk()
        => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateAuthenticatedClient().GetAsync("api/CorporateEvent")).StatusCode);
}
