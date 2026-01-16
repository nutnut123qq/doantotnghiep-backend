using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using StockInvestment.Api.Hubs;
using StockInvestment.Api.Middleware;
using StockInvestment.Infrastructure.Hubs;
using Serilog;

namespace StockInvestment.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Configure the HTTP request pipeline
    /// </summary>
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("AllowAll");

        // Use Correlation ID Middleware (first to track all requests)
        app.UseMiddleware<Middleware.CorrelationIdMiddleware>();

        // Use Security Headers Middleware (after CORS, before authentication)
        app.UseSecurityHeaders();

        // Use Rate Limiting Middleware (after security headers, before request processing)
        app.UseRateLimiting();

        // Use Serilog Request Logging
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
            };
        });

        // Use Response Compression (must be before other middleware)
        app.UseResponseCompression();

        app.UseHttpsRedirection();

        // Use Response Caching
        app.UseResponseCaching();

        // Use new Global Exception Handler Middleware
        app.UseMiddleware<Middleware.GlobalExceptionHandlerMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        // Analytics middleware
        app.UseAnalytics();

        return app;
    }

    /// <summary>
    /// Map health check endpoints
    /// </summary>
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        // Map Health Checks endpoint
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            AllowCachingResponses = false
        });

        // Map detailed health checks endpoint
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // Exclude all checks, just return 200 if app is running
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return app;
    }

    /// <summary>
    /// Map API endpoints and SignalR hubs
    /// </summary>
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapHub<StockPriceHub>("/hubs/stock-price");
        app.MapHub<TradingHub>("/hubs/trading");

        return app;
    }
}
