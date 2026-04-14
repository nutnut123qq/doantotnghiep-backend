using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AdminFinancialReportsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminFinancialReportsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PatchFinancialReport_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PatchAsync(
            $"api/admin/financial-reports/{Guid.NewGuid()}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchFinancialReport_WithNonAdmin_ReturnsForbidden()
    {
        var response = await _factory.CreateAuthenticatedClient().PatchAsync(
            $"api/admin/financial-reports/{Guid.NewGuid()}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchFinancialReport_WithAdmin_TogglesIsDeleted()
    {
        var reportId = Guid.NewGuid();
        var tickerId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StockTickers.Add(new StockTicker
            {
                Id = tickerId,
                Symbol = "VNM",
                Name = "Vinamilk",
                Exchange = Exchange.HOSE
            });
            db.FinancialReports.Add(new FinancialReport
            {
                Id = reportId,
                TickerId = tickerId,
                ReportType = "Quarterly",
                Year = 2025,
                Quarter = 1,
                Content = "{}",
                ReportDate = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var hide = await client.PatchAsync(
            $"api/admin/financial-reports/{reportId}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.FinancialReports.FindAsync(reportId);
            Assert.NotNull(row);
            Assert.True(row!.IsDeleted);
        }

        var show = await client.PatchAsync(
            $"api/admin/financial-reports/{reportId}",
            JsonContent.Create(new { isDeleted = false }));
        Assert.Equal(HttpStatusCode.NoContent, show.StatusCode);
    }

    [Fact]
    public async Task GetFinancialReports_WithAdmin_ReturnsOk()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var response = await client.GetAsync("api/admin/financial-reports?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
