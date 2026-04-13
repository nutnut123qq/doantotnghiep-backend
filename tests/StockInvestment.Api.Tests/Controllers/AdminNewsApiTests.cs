using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;
using Xunit;

namespace StockInvestment.Api.Tests.Controllers;

public class AdminNewsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminNewsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PatchNews_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PatchAsync(
            $"api/admin/news/{Guid.NewGuid()}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchNews_WithNonAdmin_ReturnsForbidden()
    {
        var response = await _factory.CreateAuthenticatedClient().PatchAsync(
            $"api/admin/news/{Guid.NewGuid()}",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchNews_WithAdmin_Missing_ReturnsNotFound()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var response = await client.PatchAsync(
            "api/admin/news/00000000-0000-0000-0000-000000000042",
            JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchNews_WithAdmin_SetsIsDeleted_KeepsRow_ThenClearsFlag()
    {
        var newsId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.News.Add(new News
            {
                Id = newsId,
                Title = "Test headline",
                Content = "Test body",
                Source = "test",
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(role: "Admin");

        var hide = await client.PatchAsync($"api/admin/news/{newsId}", JsonContent.Create(new { isDeleted = true }));
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.News.FindAsync(newsId);
            Assert.NotNull(row);
            Assert.True(row!.IsDeleted);
        }

        var show = await client.PatchAsync($"api/admin/news/{newsId}", JsonContent.Create(new { isDeleted = false }));
        Assert.Equal(HttpStatusCode.NoContent, show.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.News.FindAsync(newsId);
            Assert.NotNull(row);
            Assert.False(row!.IsDeleted);
        }
    }

    [Fact]
    public async Task GetNews_WithAdmin_ReturnsOk()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin");
        var response = await client.GetAsync("api/admin/news?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
