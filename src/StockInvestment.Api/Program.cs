using Serilog;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Domain.ValueObjects;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting StockInvestment.Api application...");

// Add services using extension methods
builder.Services
    .AddApiServices(builder.Configuration)
    .AddApplicationServices()
    .AddAuthenticationServices(builder.Configuration)
    .AddInfrastructureServices(builder.Configuration)
    .AddRepositories()
    .AddBusinessServices()
    .AddHttpClients(builder.Configuration)
    .AddMessagingServices(builder.Configuration)
    .AddBackgroundJobs()
    .AddHealthChecks(builder.Configuration)
    .AddMiddlewareOptions(builder.Configuration);

var app = builder.Build();

try
{
    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    Log.Information("Applying database migrations");
    await dbContext.Database.MigrateAsync();
    await EnsureDefaultAdminUserAsync(migrationScope.ServiceProvider, dbContext);

    // Configure pipeline using extension methods
    app.ConfigurePipeline()
       .MapHealthCheckEndpoints()
       .MapEndpoints();

    Log.Information("Starting web application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static async Task EnsureDefaultAdminUserAsync(IServiceProvider serviceProvider, ApplicationDbContext dbContext)
{
    const string adminEmailValue = "admin@gmail.com";
    const string adminPassword = "Anhyeuem12313#";

    var hasAdmin = await dbContext.Users.AnyAsync(u => u.Role == UserRole.Admin);
    if (hasAdmin)
    {
        return;
    }

    var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
    var adminEmail = Email.Create(adminEmailValue);
    var existingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

    if (existingUser is null)
    {
        var adminUser = new User
        {
            Email = adminEmail,
            PasswordHash = passwordHasher.HashPassword(adminPassword),
            Role = UserRole.Admin,
            IsEmailVerified = true,
            IsActive = true,
            LockoutEnabled = false,
            LockoutEnd = null
        };

        await dbContext.Users.AddAsync(adminUser);
    }
    else
    {
        existingUser.PasswordHash = passwordHasher.HashPassword(adminPassword);
        existingUser.Role = UserRole.Admin;
        existingUser.IsEmailVerified = true;
        existingUser.IsActive = true;
        existingUser.LockoutEnabled = false;
        existingUser.LockoutEnd = null;
        existingUser.UpdatedAt = DateTime.UtcNow;
    }

    await dbContext.SaveChangesAsync();
    Log.Information("Default admin account is ensured for {AdminEmail}", adminEmailValue);
}

// Expose Program for WebApplicationFactory<Program> in integration tests (top-level statements generate internal class otherwise)
public partial class Program { }
