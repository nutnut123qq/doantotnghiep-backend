using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class FinancialReportApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FinancialReportApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetBySymbol_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/FinancialReport/symbol/VNM")).StatusCode);

    [Fact]
    public async Task GetBySymbol_WithAuth_ReturnsOkOrNotFound()
        => Assert.True((await _factory.CreateAuthenticatedClient().GetAsync("api/FinancialReport/symbol/VNM")).StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);
}
