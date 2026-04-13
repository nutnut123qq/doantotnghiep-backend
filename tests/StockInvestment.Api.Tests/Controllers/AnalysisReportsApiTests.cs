using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AnalysisReportsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AnalysisReportsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetReports_WithoutAuth_ReturnsUnauthorized()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("api/analysis-reports")).StatusCode);

    [Fact]
    public async Task GetReports_WithAuth_ReturnsNotUnauthorized()
    {
        var response = await _factory.CreateAuthenticatedClient().GetAsync("api/analysis-reports");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
