using StockInvestment.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace StockInvestment.Api.Middleware;

/// <summary>
/// Global exception handler middleware that catches all exceptions and returns appropriate HTTP responses
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                new ErrorResponse
                {
                    Error = "Validation failed",
                    Details = validationEx.Errors,
                    TraceId = context.TraceIdentifier
                }),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                new ErrorResponse
                {
                    Error = notFoundEx.Message,
                    TraceId = context.TraceIdentifier
                }),

            UnauthorizedException unauthorizedEx => (
                StatusCodes.Status401Unauthorized,
                new ErrorResponse
                {
                    Error = unauthorizedEx.Message,
                    TraceId = context.TraceIdentifier
                }),

            ForbiddenException forbiddenEx => (
                StatusCodes.Status403Forbidden,
                new ErrorResponse
                {
                    Error = forbiddenEx.Message,
                    TraceId = context.TraceIdentifier
                }),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                new ErrorResponse
                {
                    Error = conflictEx.Message,
                    TraceId = context.TraceIdentifier
                }),

            ExternalServiceException externalServiceEx => (
                externalServiceEx.StatusCode ?? StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse
                {
                    Error = externalServiceEx.Message,
                    Details = _environment.IsDevelopment() ? new { ServiceName = externalServiceEx.ServiceName, StatusCode = externalServiceEx.StatusCode } : null,
                    TraceId = context.TraceIdentifier
                }),

            TimeoutException timeoutEx => (
                StatusCodes.Status504GatewayTimeout,
                new ErrorResponse
                {
                    Error = "Request timeout. The operation took too long to complete.",
                    TraceId = context.TraceIdentifier
                }),

            TaskCanceledException canceledEx => (
                StatusCodes.Status504GatewayTimeout,
                new ErrorResponse
                {
                    Error = "Request was canceled or timed out.",
                    TraceId = context.TraceIdentifier
                }),

            InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) || 
                                                         invalidOpEx.Message.Contains("mock data", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status503ServiceUnavailable, // P0-3: Return 503 for data unavailable when mock is disabled
                new ErrorResponse
                {
                    Error = invalidOpEx.Message,
                    TraceId = context.TraceIdentifier
                }),

            InvalidOperationException invalidOpEx => (
                StatusCodes.Status400BadRequest,
                new ErrorResponse
                {
                    Error = invalidOpEx.Message,
                    TraceId = context.TraceIdentifier
                }),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Error = _environment.IsDevelopment()
                        ? exception.Message
                        : "An unexpected error occurred. Please try again later.",
                    Details = _environment.IsDevelopment() ? exception.StackTrace : null,
                    TraceId = context.TraceIdentifier
                })
        };

        _logger.LogError(
            exception,
            "Exception occurred: {Message} | TraceId: {TraceId}",
            exception.Message,
            context.TraceIdentifier);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

/// <summary>
/// Standard error response model
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = null!;
    public object? Details { get; set; }
    public string? TraceId { get; set; }
}

