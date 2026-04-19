using System.Text.RegularExpressions;
using Serilog;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    await EnsureDefaultAdminUserAsync(migrationScope.ServiceProvider, dbContext, app.Configuration);

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

static bool IsBootstrapPasswordStrongEnough(string password, out string failureDetail)
{
    failureDetail = "";
    if (password.Length is < 8 or > 100)
    {
        failureDetail = "Password must be between 8 and 100 characters.";
        return false;
    }

    if (!Regex.IsMatch(password, "[A-Z]")) { failureDetail = "Password must contain at least one uppercase letter."; return false; }
    if (!Regex.IsMatch(password, "[a-z]")) { failureDetail = "Password must contain at least one lowercase letter."; return false; }
    if (!Regex.IsMatch(password, "[0-9]")) { failureDetail = "Password must contain at least one digit."; return false; }
    if (!Regex.IsMatch(password, "[^a-zA-Z0-9]")) { failureDetail = "Password must contain at least one special character."; return false; }
    return true;
}

static async Task EnsureDefaultAdminUserAsync(
    IServiceProvider serviceProvider,
    ApplicationDbContext dbContext,
    IConfiguration configuration)
{
    var hasAdmin = await dbContext.Users.AnyAsync(u => u.Role == UserRole.Admin);
    if (hasAdmin)
    {
        return;
    }

    var adminEmailValue = configuration["BootstrapAdmin:Email"]?.Trim();
    if (string.IsNullOrEmpty(adminEmailValue))
    {
        adminEmailValue = "admin@example.com";
    }

    var adminPassword = configuration["BootstrapAdmin:Password"]?.Trim();
    if (string.IsNullOrEmpty(adminPassword))
    {
        Log.Warning(
            "No Admin user exists and BootstrapAdmin:Password is not set. Skipping default admin seed. " +
            "Set user secrets (Development): dotnet user-secrets set \"BootstrapAdmin:Password\" \"<strong-password>\" " +
            "and optionally \"BootstrapAdmin:Email\". Production: set env BootstrapAdmin__Password / BootstrapAdmin__Email.");
        return;
    }

    if (!IsBootstrapPasswordStrongEnough(adminPassword, out var pwdReason))
    {
        Log.Warning("BootstrapAdmin:Password is invalid ({Reason}). Skipping default admin seed.", pwdReason);
        return;
    }

    Email adminEmail;
    try
    {
        adminEmail = Email.Create(adminEmailValue);
    }
    catch (ArgumentException ex)
    {
        Log.Warning(ex, "BootstrapAdmin:Email is invalid ({Email}). Skipping default admin seed.", adminEmailValue);
        return;
    }

    var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
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
