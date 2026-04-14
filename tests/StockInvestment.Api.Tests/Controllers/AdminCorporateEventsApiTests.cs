using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AdminCorporateEventsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminCorporateEventsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PatchCorporateEvent_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PatchAsync(
            $"api/admin/corporate-events/{Guid.NewGuid()}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchCorporateEvent_WithNonAdmin_ReturnsForbidden()
    {
        var response = await _factory.CreateAuthenticatedClient().PatchAsync(
            $"api/admin/corporate-events/{Guid.NewGuid()}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchCorporateEvent_WithAdmin_TogglesIsDeleted()
    {
        var eventId = Guid.NewGuid();
        var tickerId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StockTickers.Add(new StockTicker
            {
                Id = tickerId,
                Symbol = "FPT",
                Name = "FPT Corp",
                Exchange = Exchange.HOSE
            });
            db.CorporateEvents.Add(new EarningsEvent
            {
                Id = eventId,
                StockTickerId = tickerId,
                Title = "Q1 Earnings",
                EventDate = DateTime.UtcNow,
                Period = "Q1",
                Year = 2025,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var hide = await client.PatchAsync(
            $"api/admin/corporate-events/{eventId}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.CorporateEvents.FindAsync(eventId);
            Assert.NotNull(row);
            Assert.True(row!.IsDeleted);
        }

        var show = await client.PatchAsync(
            $"api/admin/corporate-events/{eventId}",
            JsonContent.Create(new { isDeleted = false }));
        Assert.Equal(HttpStatusCode.NoContent, show.StatusCode);
    }

    [Fact]
    public async Task GetCorporateEvents_WithAdmin_ReturnsOk()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var response = await client.GetAsync("api/admin/corporate-events?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
