using Microsoft.Extensions.Options;

namespace StockInvestment.Api.Middleware;

/// <summary>
/// Middleware to add security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<SecurityHeadersOptions> options,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", _options.XFrameOptions);
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", _options.ReferrerPolicy);

        // Add Strict-Transport-Security (HSTS) only for HTTPS
        if (context.Request.IsHttps)
        {
            var hstsValue = $"max-age={_options.HstsMaxAge}";
            if (_options.HstsIncludeSubDomains)
            {
                hstsValue += "; includeSubDomains";
            }
            if (_options.HstsPreload)
            {
                hstsValue += "; preload";
            }
            context.Response.Headers.Append("Strict-Transport-Security", hstsValue);
        }

        // Add Content-Security-Policy if configured
        if (!string.IsNullOrEmpty(_options.ContentSecurityPolicy))
        {
            context.Response.Headers.Append("Content-Security-Policy", _options.ContentSecurityPolicy);
        }

        // Add Permissions-Policy if configured
        if (!string.IsNullOrEmpty(_options.PermissionsPolicy))
        {
            context.Response.Headers.Append("Permissions-Policy", _options.PermissionsPolicy);
        }

        await _next(context);
    }
}

/// <summary>
/// Configuration options for security headers
/// </summary>
public class SecurityHeadersOptions
{
    public string XFrameOptions { get; set; } = "DENY";
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
    public int HstsMaxAge { get; set; } = 31536000; // 1 year in seconds
    public bool HstsIncludeSubDomains { get; set; } = true;
    public bool HstsPreload { get; set; } = false;
    public string? ContentSecurityPolicy { get; set; }
    public string? PermissionsPolicy { get; set; }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
