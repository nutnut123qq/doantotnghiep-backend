using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Service for creating resilience policies (retry, circuit breaker)
/// </summary>
public class ResiliencePolicyService
{
    private readonly ILogger<ResiliencePolicyService> _logger;

    public ResiliencePolicyService(ILogger<ResiliencePolicyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create retry policy with exponential backoff
    /// </summary>
    public AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy(int retryCount = 3)
    {
        // Do not retry 429 (quota / rate limit): extra attempts usually waste slots without helping.
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} after {Delay}ms. Status: {StatusCode}",
                        retryCount,
                        timespan.TotalMilliseconds,
                        outcome.Result?.StatusCode);
                });
    }

    /// <summary>
    /// Create circuit breaker policy
    /// </summary>
    public AsyncCircuitBreakerPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy(
        int exceptionsAllowedBeforeBreaking = 5,
        TimeSpan durationOfBreak = default)
    {
        if (durationOfBreak == default)
        {
            durationOfBreak = TimeSpan.FromSeconds(30);
        }

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking,
                durationOfBreak,
                onBreak: (result, duration) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for {Duration}ms. Status: {StatusCode}",
                        duration.TotalMilliseconds,
                        result.Result?.StatusCode);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open");
                });
    }

    /// <summary>
    /// Create combined policy (retry + circuit breaker)
    /// </summary>
    public IAsyncPolicy<HttpResponseMessage> CreateCombinedPolicy(
        int retryCount = 3,
        int exceptionsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        var retryPolicy = CreateRetryPolicy(retryCount);
        var circuitBreakerPolicy = CreateCircuitBreakerPolicy(
            exceptionsAllowedBeforeBreaking,
            durationOfBreak ?? TimeSpan.FromSeconds(30));

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }
}
