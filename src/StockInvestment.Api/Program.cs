using Serilog;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Api.Extensions;
using Microsoft.EntityFrameworkCore;

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

// Expose Program for WebApplicationFactory<Program> in integration tests (top-level statements generate internal class otherwise)
public partial class Program { }
