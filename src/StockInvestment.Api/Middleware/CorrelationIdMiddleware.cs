namespace StockInvestment.Api.Middleware;

/// <summary>
/// Middleware to add correlation ID to each request for distributed tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get correlation ID from request header or generate new one
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // Add correlation ID to response headers
        context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);

        // Add correlation ID to HttpContext items for access in other middleware/controllers
        context.Items["CorrelationId"] = correlationId;

        // Add correlation ID to logging scope
        using (context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger<CorrelationIdMiddleware>()
            .BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestPath"] = context.Request.Path,
                ["RequestMethod"] = context.Request.Method
            }))
        {
            await _next(context);
        }
    }
}

