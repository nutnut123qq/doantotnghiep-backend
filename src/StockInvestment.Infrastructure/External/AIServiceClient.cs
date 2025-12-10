using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using System.Net.Http.Json;

namespace StockInvestment.Infrastructure.External;

public class AIServiceClient : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIServiceClient> _logger;

    public AIServiceClient(HttpClient httpClient, ILogger<AIServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> SummarizeNewsAsync(string newsContent)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/summarize", new { content = newsContent });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SummarizeResponse>();
            return result?.Summary ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for news summarization");
            throw;
        }
    }

    public async Task<string> AnalyzeEventAsync(string eventDescription)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analyze-event", new { description = eventDescription });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AnalyzeResponse>();
            return result?.Analysis ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for event analysis");
            throw;
        }
    }

    public async Task<object> GenerateForecastAsync(Guid tickerId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/forecast", new { tickerId });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<object>() ?? new { };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for forecast generation");
            throw;
        }
    }

    public async Task<string> AnswerQuestionAsync(string question, string context)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/qa", new { question, context });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<QAResponse>();
            return result?.Answer ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for Q&A");
            throw;
        }
    }

    public async Task<object> ParseAlertIntentAsync(string naturalLanguageInput)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/parse-alert", new { input = naturalLanguageInput });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<object>() ?? new { };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for alert parsing");
            throw;
        }
    }

    private record SummarizeResponse(string Summary);
    private record AnalyzeResponse(string Analysis);
    private record QAResponse(string Answer);
}

