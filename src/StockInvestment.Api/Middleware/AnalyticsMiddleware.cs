using StockInvestment.Application.Interfaces;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace StockInvestment.Api.Middleware;

/// <summary>
/// Middleware to track API requests for analytics
/// </summary>
public class AnalyticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AnalyticsMiddleware> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public AnalyticsMiddleware(
        RequestDelegate next, 
        ILogger<AnalyticsMiddleware> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Track the request (fire and forget with proper error handling and cancellation)
            // Create a new scope for the background task to avoid DbContext disposal issues
            _ = Task.Run(async () =>
            {
                // Capture values before the request context is disposed
                var endpoint = $"{context.Request.Path}{context.Request.QueryString}";
                var method = context.Request.Method;
                var statusCode = context.Response.StatusCode;
                var responseTime = stopwatch.ElapsedMilliseconds;
                var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Create a new scope for the background task
                using var scope = _serviceScopeFactory.CreateScope();
                try
                {
                    var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                    await analyticsService.TrackApiRequestAsync(endpoint, method, statusCode, responseTime, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error tracking API request in background task");
                    // Don't rethrow - this is fire-and-forget, but we log the error
                }
            }, context.RequestAborted); // Use request cancellation token
        }
    }
}

public static class AnalyticsMiddlewareExtensions
{
    public static IApplicationBuilder UseAnalytics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AnalyticsMiddleware>();
    }
}
