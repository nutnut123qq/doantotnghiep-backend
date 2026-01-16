using System.Net;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Middleware;

/// <summary>
/// Middleware to implement rate limiting based on IP address and endpoint using Redis
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ICacheService _cacheService;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        ILogger<RateLimitingMiddleware> logger,
        ICacheService cacheService)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableRateLimiting)
        {
            await _next(context);
            return;
        }

        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        var endpoint = context.Request.Path.Value ?? string.Empty;
        var key = GetRateLimitKey(clientIp, endpoint);
        var limit = GetRateLimitForEndpoint(endpoint);

        // Use Redis for atomic increment with sliding window (60 seconds)
        var count = await _cacheService.IncrementAsync(key, TimeSpan.FromMinutes(1));

        if (count > limit)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {ClientIp} on {Endpoint}. Requests: {Count}/{Limit}",
                clientIp, endpoint, count, limit);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append("X-RateLimit-Limit", limit.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", "0");
            context.Response.Headers.Append("Retry-After", "60");

            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        // Add rate limit headers
        context.Response.Headers.Append("X-RateLimit-Limit", limit.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining", Math.Max(0, limit - (int)count).ToString());

        await _next(context);
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (when behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string GetRateLimitKey(string clientIp, string endpoint)
    {
        // Use different keys for different endpoints to allow per-endpoint rate limiting
        if (endpoint.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            return $"rate_limit:auth:{clientIp}";
        }

        return $"rate_limit:global:{clientIp}";
    }

    private int GetRateLimitForEndpoint(string endpoint)
    {
        if (endpoint.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            return _options.AuthRequestsPerMinute;
        }

        if (endpoint.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return _options.ApiRequestsPerMinute;
        }

        return _options.GlobalRequestsPerMinute;
    }
}

/// <summary>
/// Configuration options for rate limiting
/// </summary>
public class RateLimitingOptions
{
    public int GlobalRequestsPerMinute { get; set; } = 100;
    public int AuthRequestsPerMinute { get; set; } = 5;
    public int ApiRequestsPerMinute { get; set; } = 60;
    public bool EnableRateLimiting { get; set; } = true;
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
