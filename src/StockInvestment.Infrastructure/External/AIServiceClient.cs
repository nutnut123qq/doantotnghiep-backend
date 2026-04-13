using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.DTOs.AnalysisReports;
using System.Net;
using System.Text.Json.Serialization;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Client for AI service endpoints.
/// HttpClient BaseAddress is configured as "http://localhost:8000" (without /api)
/// All endpoints use absolute paths like "/api/qa", "/api/summarize", etc.
/// </summary>
public partial class AIServiceClient : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIServiceClient> _logger;

    public AIServiceClient(HttpClient httpClient, ILogger<AIServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private static DateTime ParseGeneratedAt(string? generatedAt)
    {
        if (string.IsNullOrWhiteSpace(generatedAt))
        {
            return DateTime.UtcNow;
        }

        if (DateTime.TryParse(generatedAt, out var parsedDate))
        {
            return parsedDate;
        }

        // If parsing fails, return current time
        return DateTime.UtcNow;
    }

    /// <summary>
    /// Ensure HttpClient BaseAddress is configured
    /// </summary>
    private void EnsureBaseAddressConfigured()
    {
        if (_httpClient.BaseAddress == null)
        {
            throw new InvalidOperationException("HttpClient BaseAddress is not configured. Please check AIService configuration.");
        }
    }

    /// <summary>
    /// Check if error content indicates a quota exceeded error
    /// </summary>
    private static bool IsQuotaError(string errorContent)
    {
        return errorContent.Contains("429") || 
               errorContent.Contains("quota", StringComparison.OrdinalIgnoreCase) || 
               errorContent.Contains("Quota exceeded", StringComparison.OrdinalIgnoreCase) || 
               errorContent.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRateLimitOrBillingError(string errorContent)
    {
        return errorContent.Contains("429") ||
               errorContent.Contains("402") ||
               errorContent.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               errorContent.Contains("free-models-per-day", StringComparison.OrdinalIgnoreCase) ||
               errorContent.Contains("free-models-per-min", StringComparison.OrdinalIgnoreCase) ||
               errorContent.Contains("spend limit exceeded", StringComparison.OrdinalIgnoreCase) ||
               errorContent.Contains("credits", StringComparison.OrdinalIgnoreCase) ||
               errorContent.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Handle HTTP error response and throw appropriate exception
    /// </summary>
    private async Task HandleHttpErrorAsync(HttpResponseMessage response, string endpoint, string? symbol = null)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        var statusCode = response.StatusCode;
        
        // Check if it's a quota exceeded error (may be wrapped in 500 from AI service)
        bool isQuotaError = IsQuotaError(errorContent);
        bool isRateLimitOrBillingError = IsRateLimitOrBillingError(errorContent) ||
                                         statusCode == HttpStatusCode.TooManyRequests ||
                                         (int)statusCode == 402;
        
        // Extract meaningful error message
        string errorMessage = errorContent;
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            if (isQuotaError)
            {
                errorMessage = $"Quota exceeded: {errorContent}";
                statusCode = HttpStatusCode.TooManyRequests; // 429
            }
            // Check if it's a Gemini API error
            else if (errorContent.Contains("gemini", StringComparison.OrdinalIgnoreCase) || 
                     errorContent.Contains("models/", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Gemini API error: {errorContent}";
            }
            else
            {
                errorMessage = $"AI service internal error: {errorContent}";
            }
        }
        
        // Log based on status code type
        if (statusCode == HttpStatusCode.NotFound)
        {
            var logMessage = symbol != null 
                ? "AI service endpoint {Endpoint} not found (404) for {Symbol}. Response: {Response}"
                : "AI service endpoint {Endpoint} not found (404). Response: {Response}";
            if (symbol != null)
                _logger.LogWarning(logMessage, endpoint, symbol, errorContent);
            else
                _logger.LogWarning(logMessage, endpoint, errorContent);
        }
        else if (isQuotaError || statusCode == HttpStatusCode.TooManyRequests)
        {
            var logMessage = symbol != null
                ? "AI service quota exceeded (429) for {Endpoint} with {Symbol}. Response: {Response}"
                : "AI service quota exceeded (429) for {Endpoint}. Response: {Response}";
            if (symbol != null)
                _logger.LogWarning(logMessage, endpoint, symbol, errorContent);
            else
                _logger.LogWarning(logMessage, endpoint, errorContent);
        }
        else if (statusCode == HttpStatusCode.InternalServerError)
        {
            var logMessage = symbol != null
                ? "AI service returned InternalServerError (500) for {Endpoint} with {Symbol}. This may indicate a Gemini API issue. Response: {Response}"
                : "AI service returned InternalServerError (500) for {Endpoint}. This may indicate a Gemini API issue. Response: {Response}";
            if (symbol != null)
                _logger.LogError(logMessage, endpoint, symbol, errorContent);
            else
                _logger.LogError(logMessage, endpoint, errorContent);
        }
        else
        {
            var logMessage = symbol != null
                ? "AI service returned {StatusCode} for {Endpoint} with {Symbol}. Response: {Response}"
                : "AI service returned {StatusCode} for {Endpoint}. Response: {Response}";
            if (symbol != null)
                _logger.LogWarning(logMessage, statusCode, endpoint, symbol, errorContent);
            else
                _logger.LogWarning(logMessage, statusCode, endpoint, errorContent);
        }
        
        if (isRateLimitOrBillingError)
        {
            throw new Domain.Exceptions.ExternalServiceException(
                "AI Service",
                "AI đang quá tải hoặc đã chạm giới hạn sử dụng. Vui lòng thử lại sau ít phút.",
                StatusCodes.Status429TooManyRequests);
        }

        // Create exception with status code information
        var exception = new HttpRequestException($"AI service returned {statusCode}: {errorMessage}")
        {
            Data = { ["StatusCode"] = (int)statusCode }
        };
        throw exception;
    }

    /// <summary>
    /// Parse forecast response from API
    /// </summary>
    private ForecastResult ParseForecastResponse(ForecastApiResponse? result)
    {
        if (result == null)
        {
            throw new Domain.Exceptions.ExternalServiceException("AI Service", "Failed to get forecast from AI service");
        }

        return new ForecastResult
        {
            Symbol = result.Symbol,
            Trend = result.Trend,
            Confidence = result.Confidence,
            ConfidenceScore = result.ConfidenceScore,
            TimeHorizon = result.TimeHorizon,
            Recommendation = result.Recommendation,
            KeyDrivers = result.KeyDrivers,
            Risks = result.Risks,
            Analysis = result.Analysis,
            GeneratedAt = ParseGeneratedAt(result.GeneratedAt)
        };
    }

    private record SummarizeResponse(string Summary);
    private record SummarizeDetailedResponse(
        string Summary,
        string Sentiment,
        string? ImpactAssessment,
        string? Impact_Assessment,
        List<string>? KeyPoints,
        List<string>? Key_Points
    );
    private record AnalyzeResponse(string Analysis);
    private record AnalyzeEventDetailedResponse(string Analysis, string Impact);
    
    /// <summary>
    /// Response from AI service /api/qa endpoint with source objects
    /// </summary>
    private sealed class QAWithSourcesResponse
    {
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("sources")]
        public List<SourceObject> Sources { get; set; } = new();
    }
    private record ParseAlertApiResponse(string Ticker, string Condition, decimal Threshold, string Timeframe, string AlertType);
    private sealed class ForecastApiResponse
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("trend")]
        public string Trend { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public string Confidence { get; set; } = string.Empty;

        [JsonPropertyName("confidence_score")]
        public double ConfidenceScore { get; set; }

        [JsonPropertyName("time_horizon")]
        public string TimeHorizon { get; set; } = string.Empty;

        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; } = string.Empty;

        [JsonPropertyName("key_drivers")]
        public List<string> KeyDrivers { get; set; } = new();

        [JsonPropertyName("risks")]
        public List<string> Risks { get; set; } = new();

        [JsonPropertyName("analysis")]
        public string Analysis { get; set; } = string.Empty;

        [JsonPropertyName("generated_at")]
        public string GeneratedAt { get; set; } = string.Empty;
    }

    private record InsightApiResponse(
        string Symbol,
        string Type,
        string Title,
        string Description,
        int Confidence,
        List<string> Reasoning,
        decimal? TargetPrice,
        decimal? StopLoss,
        string GeneratedAt,
        List<string>? Evidence,
        Dictionary<string, string>? Metadata
    );
}

