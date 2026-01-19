using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Contracts.AI;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Client for AI service endpoints.
/// HttpClient BaseAddress is configured as "http://localhost:8000" (without /api)
/// All endpoints use absolute paths like "/api/qa", "/api/summarize", etc.
/// </summary>
public class AIServiceClient : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIServiceClient> _logger;

    public AIServiceClient(HttpClient httpClient, ILogger<AIServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> SummarizeNewsAsync(string newsContent, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/summarize", new { content = newsContent }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SummarizeResponse>(
                cancellationToken: cancellationToken);
            return result?.Summary ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for news summarization");
            throw;
        }
    }

    public async Task<NewsSummaryResult> SummarizeNewsDetailedAsync(string newsContent, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/summarize", 
                new { content = newsContent }, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<SummarizeDetailedResponse>(
                cancellationToken: cancellationToken);
            
            return new NewsSummaryResult
            {
                Summary = result?.Summary ?? string.Empty,
                Sentiment = result?.Sentiment ?? "neutral",
                ImpactAssessment = result?.ImpactAssessment ?? result?.Impact_Assessment ?? string.Empty,
                KeyPoints = result?.KeyPoints ?? result?.Key_Points ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for detailed news summarization");
            throw;
        }
    }

    public async Task<string> AnalyzeEventAsync(string eventDescription, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analyze-event", new { description = eventDescription }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(
                cancellationToken: cancellationToken);
            return result?.Analysis ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for event analysis");
            throw;
        }
    }

    public async Task<EventAnalysisResult> AnalyzeEventDetailedAsync(string eventDescription, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analyze-event", 
                new { description = eventDescription }, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<AnalyzeEventDetailedResponse>(
                cancellationToken: cancellationToken);
            
            return new EventAnalysisResult
            {
                Analysis = result?.Analysis ?? string.Empty,
                Impact = result?.Impact ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for detailed event analysis");
            throw;
        }
    }

    public async Task<object> GenerateForecastAsync(Guid tickerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/forecast", new { tickerId }, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<object>(
                cancellationToken: cancellationToken) ?? new { };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for forecast generation");
            throw;
        }
    }

    public async Task<QuestionAnswerResult> AnswerQuestionAsync(string question, string context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: Endpoint is "/api/qa" because HttpClient BaseAddress is "http://localhost:8000"
            var response = await _httpClient.PostAsJsonAsync("/api/qa", new { question, context }, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<QADetailedResponse>(
                cancellationToken: cancellationToken);
            
            return new QuestionAnswerResult
            {
                Answer = result?.Answer ?? string.Empty,
                Sources = result?.Sources ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for Q&A");
            throw;
        }
    }

    public async Task<ParsedAlert> ParseAlertAsync(string naturalLanguageInput, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/parse-alert", new { input = naturalLanguageInput }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ParseAlertApiResponse>(
                cancellationToken: cancellationToken);

            if (result == null)
            {
                throw new Domain.Exceptions.ExternalServiceException("AI Service", "Failed to parse alert from AI service");
            }

            return new ParsedAlert
            {
                Symbol = result.Ticker,
                Type = result.AlertType,
                Operator = result.Condition.Contains("above") || result.Condition.Contains("greater") ? ">" :
                          result.Condition.Contains("below") || result.Condition.Contains("less") ? "<" : "=",
                Value = result.Threshold,
                Timeframe = result.Timeframe
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for alert parsing");
            throw;
        }
    }

    public async Task<ForecastResult> GenerateForecastBySymbolAsync(string symbol, string timeHorizon = "short", CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureBaseAddressConfigured();

            var endpoint = $"/api/forecast/{symbol}";
            var response = await _httpClient.GetAsync($"{endpoint}?time_horizon={timeHorizon}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response, endpoint, symbol);
            }
            
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ForecastApiResponse>(
                cancellationToken: cancellationToken);

            return ParseForecastResponse(result);
        }
        catch (HttpRequestException httpEx) when (httpEx.Data.Contains("StatusCode") && (int)httpEx.Data["StatusCode"]! == 404)
        {
            _logger.LogWarning("AI service endpoint /api/forecast/{Symbol} not found (404). AI service may not be running or endpoint path is incorrect.", symbol);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for forecast generation");
            throw;
        }
    }

    public async Task<ForecastResult> GenerateForecastWithDataAsync(string symbol, string timeHorizon, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureBaseAddressConfigured();

            var request = new
            {
                symbol = symbol,
                technical_data = technicalData ?? new Dictionary<string, string>(),
                fundamental_data = fundamentalData ?? new Dictionary<string, string>(),
                sentiment_data = sentimentData ?? new Dictionary<string, string>(),
                time_horizon = timeHorizon
            };

            var endpoint = "/api/forecast/generate";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response, endpoint, symbol);
            }
            
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ForecastApiResponse>(
                cancellationToken: cancellationToken);

            return ParseForecastResponse(result);
        }
        catch (HttpRequestException httpEx) when (httpEx.Data.Contains("StatusCode") && (int)httpEx.Data["StatusCode"]! == 404)
        {
            _logger.LogWarning("AI service endpoint /api/forecast/generate not found (404). AI service may not be running or endpoint path is incorrect.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for forecast generation with real data");
            throw;
        }
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

    /// <summary>
    /// Handle HTTP error response and throw appropriate exception
    /// </summary>
    private async Task HandleHttpErrorAsync(HttpResponseMessage response, string endpoint, string? symbol = null)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        var statusCode = response.StatusCode;
        
        // Check if it's a quota exceeded error (may be wrapped in 500 from AI service)
        bool isQuotaError = IsQuotaError(errorContent);
        
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
        string? Impact_Assessment,  // snake_case version from FastAPI
        List<string>? KeyPoints,
        List<string>? Key_Points  // snake_case version from FastAPI
    );
    private record AnalyzeResponse(string Analysis);
    private record AnalyzeEventDetailedResponse(string Analysis, string Impact);
    
    /// <summary>
    /// Response from AI service /api/qa endpoint
    /// Uses JsonPropertyName to ensure proper camelCase mapping from Python FastAPI
    /// </summary>
    private sealed class QADetailedResponse
    {
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("sources")]
        public List<string> Sources { get; set; } = new();
    }
    private record ParseAlertApiResponse(string Ticker, string Condition, decimal Threshold, string Timeframe, string AlertType);
    private record ForecastApiResponse(
        string Symbol,
        string Trend,
        string Confidence,
        double ConfidenceScore,
        string TimeHorizon,
        string Recommendation,
        List<string> KeyDrivers,
        List<string> Risks,
        string Analysis,
        string GeneratedAt
    );

    public async Task<InsightResult> GenerateInsightAsync(string symbol, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureBaseAddressConfigured();

            var request = new
            {
                symbol = symbol,
                technical_data = technicalData ?? new Dictionary<string, string>(),
                fundamental_data = fundamentalData ?? new Dictionary<string, string>(),
                sentiment_data = sentimentData ?? new Dictionary<string, string>()
            };

            var endpoint = "/api/insights/generate";
            _logger.LogInformation("Calling AI service {Endpoint} for symbol {Symbol}", endpoint, symbol);
            var startTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("AI service responded for {Symbol} in {ElapsedSeconds:F2} seconds", symbol, elapsed);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorAsync(response, endpoint, symbol);
            }
            
            _logger.LogInformation("Successfully received response from AI service for symbol {Symbol}", symbol);
            
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<InsightApiResponse>(
                cancellationToken: cancellationToken);

            if (result == null)
            {
                throw new Domain.Exceptions.ExternalServiceException("AI Service", "Failed to get insight from AI service - response was null");
            }

            _logger.LogInformation("Successfully parsed insight result for {Symbol}: Type={Type}, Confidence={Confidence}", 
                symbol, result.Type, result.Confidence);

            return new InsightResult
            {
                Symbol = result.Symbol,
                Type = result.Type,
                Title = result.Title,
                Description = result.Description,
                Confidence = result.Confidence,
                Reasoning = result.Reasoning,
                TargetPrice = result.TargetPrice,
                StopLoss = result.StopLoss,
                GeneratedAt = ParseGeneratedAt(result.GeneratedAt)
            };
        }
        catch (TaskCanceledException timeoutEx)
        {
            _logger.LogError(timeoutEx, "Timeout calling AI service for insight generation (symbol: {Symbol}). AI service may be slow or not responding.", symbol);
            throw new HttpRequestException($"AI service timeout after 120 seconds. Please check if AI service is running at {_httpClient.BaseAddress}", timeoutEx)
            {
                Data = { ["StatusCode"] = 408 } // Request Timeout
            };
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error calling AI service for insight generation (symbol: {Symbol})", symbol);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for insight generation (symbol: {Symbol})", symbol);
            throw;
        }
    }

    public async Task<string?> GetAlertExplanationAsync(
        string symbol,
        string alertType,
        decimal currentValue,
        decimal threshold,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = $@"Stock {symbol} triggered a {alertType} alert.
Threshold: {threshold:N0}
Current Value: {currentValue:N0}

Provide a brief 1-2 sentence explanation of what this alert means for investors. Keep it concise and actionable.";

            var response = await _httpClient.PostAsJsonAsync("/api/ai/quick-analysis", new
            {
                prompt = prompt,
                maxTokens = 100
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<AiAnalysisResponse>(cancellationToken);
            return result?.Text?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get AI explanation for {Symbol} {AlertType}", symbol, alertType);
            return null;
        }
    }

    private record AiAnalysisResponse(string? Text);

    private record InsightApiResponse(
        string Symbol,
        string Type,
        string Title,
        string Description,
        int Confidence,
        List<string> Reasoning,
        decimal? TargetPrice,
        decimal? StopLoss,
        string GeneratedAt
    );
}

