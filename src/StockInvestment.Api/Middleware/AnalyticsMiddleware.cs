using StockInvestment.Application.Interfaces;
using System.Diagnostics;
using System.Security.Claims;

namespace StockInvestment.Api.Middleware;

/// <summary>
/// Middleware to track API requests for analytics
/// </summary>
public class AnalyticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AnalyticsMiddleware> _logger;

    public AnalyticsMiddleware(RequestDelegate next, ILogger<AnalyticsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAnalyticsService analyticsService)
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
            _ = Task.Run(async () =>
            {
                try
                {
                    var endpoint = $"{context.Request.Path}{context.Request.QueryString}";
                    var method = context.Request.Method;
                    var statusCode = context.Response.StatusCode;
                    var responseTime = stopwatch.ElapsedMilliseconds;
                    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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
