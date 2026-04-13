using System.Collections.Generic;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Domain.ValueObjects;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Api.Tests.Helpers;
using StockInvestment.Api.Tests.Mocks;

namespace StockInvestment.Api.Tests;

/// <summary>
/// Test host for API integration tests: InMemory DB and in-memory cache (no Postgres/Redis).
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private bool _usersSeeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Secret"] = "integration-test-secret-key-at-least-32-characters-long",
                ["BackgroundJobs:StockPriceUpdateInitialDelaySeconds"] = "0"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL with InMemory
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            var optionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (optionsDescriptor != null)
                services.Remove(optionsDescriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));

            // Replace Redis cache with in-memory stub
            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICacheService));
            if (cacheDescriptor != null)
                services.Remove(cacheDescriptor);
            services.AddSingleton<ICacheService, InMemoryCacheService>();

            // Replace external HTTP services with mocks (no timeouts in tests)
            var aiDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIService));
            if (aiDescriptor != null)
                services.Remove(aiDescriptor);
            services.AddScoped<IAIService, MockAIService>();

            var vnStockDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IVNStockService));
            if (vnStockDescriptor != null)
                services.Remove(vnStockDescriptor);
            services.AddScoped<IVNStockService, MockVNStockService>();
        });
    }

    /// <summary>
    /// Creates an HttpClient with Bearer token for the test user (or admin). Seeds test users on first use.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid? userId = null, string? role = null)
    {
        EnsureTestUsersSeeded();
        var (id, email, roleName) = role is "Admin" or "SuperAdmin"
            ? (TestUserConstants.AdminUserId, TestUserConstants.AdminUserEmail, TestUserConstants.AdminUserRole)
            : (TestUserConstants.TestUserId, TestUserConstants.TestUserEmail, TestUserConstants.TestUserRole);
        if (userId.HasValue) id = userId.Value;
        var token = TestJwtHelper.CreateToken(id, email, roleName);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private void EnsureTestUsersSeeded()
    {
        if (_usersSeeded) return;
        lock (typeof(CustomWebApplicationFactory))
        {
            if (_usersSeeded) return;
            using var scope = Server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (db.Users.Any()) { _usersSeeded = true; return; }
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var testUser = new User
            {
                Id = TestUserConstants.TestUserId,
                Email = Email.Create(TestUserConstants.TestUserEmail),
                FullName = "Test User",
                PasswordHash = hasher.HashPassword(TestUserConstants.TestUserPassword),
                Role = UserRole.Investor,
                IsEmailVerified = true,
                IsActive = true
            };
            var adminUser = new User
            {
                Id = TestUserConstants.AdminUserId,
                Email = Email.Create(TestUserConstants.AdminUserEmail),
                FullName = "Admin User",
                PasswordHash = hasher.HashPassword(TestUserConstants.AdminUserPassword),
                Role = UserRole.Admin,
                IsEmailVerified = true,
                IsActive = true
            };
            db.Users.Add(testUser);
            db.Users.Add(adminUser);
            db.SaveChanges();
            _usersSeeded = true;
        }
    }
}
