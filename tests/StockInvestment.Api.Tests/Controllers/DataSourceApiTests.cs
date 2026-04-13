using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class DataSourceApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DataSourceApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetDataSources_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/DataSource")).StatusCode);

    [Fact]
    public async Task GetDataSources_WithAuth_ReturnsOkOrForbidden()
        => Assert.True((await _factory.CreateAuthenticatedClient().GetAsync("api/DataSource")).StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden);
}
