using Serilog;
using StockInvestment.Api.Extensions;

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

// Configure pipeline using extension methods
app.ConfigurePipeline()
   .MapHealthCheckEndpoints()
   .MapEndpoints();

try
{
    Log.Information("Starting web application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
