using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.External;

public partial class AIServiceClient
{
    public async Task<QuestionAnswerResult> AnswerQuestionAsync(
        string question,
        string baseContext,
        string? documentId = null,
        string? source = null,
        string? symbol = null,
        int topK = 6,
        CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();

        var requestBody = new
        {
            question,
            base_context = baseContext,
            top_k = topK,
            document_id = documentId,
            source,
            symbol
        };

        const string endpoint = "/api/qa";
        var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleHttpErrorAsync(response, endpoint, symbol);
        }

        QAWithSourcesResponse? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<QAWithSourcesResponse>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON from AI service {Endpoint}", endpoint);
            throw new Domain.Exceptions.ExternalServiceException(
                "AI Service",
                "AI service returned a response that could not be parsed as QA JSON.");
        }

        return new QuestionAnswerResult
        {
            Answer = result?.Answer ?? string.Empty,
            Sources = result?.Sources ?? new List<SourceObject>()
        };
    }

    public async Task<AnswerWithContextResult> AnswerWithContextPartsAsync(
        string question,
        List<ContextPart> contextParts,
        CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var endpoint = "/api/ai/answer-with-context";
        var response = await _httpClient.PostAsJsonAsync(endpoint, new { question, context_parts = contextParts }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleHttpErrorAsync(response, endpoint);
        }

        var result = await response.Content.ReadFromJsonAsync<AnswerWithContextResponse>(cancellationToken: cancellationToken);
        if (result == null)
        {
            throw new Domain.Exceptions.ExternalServiceException("AI Service", "Failed to get answer from AI service - response was null");
        }

        return new AnswerWithContextResult
        {
            Answer = result.Answer,
            UsedSources = result.UsedSources
        };
    }

    public async Task<ParsedAlert> ParseAlertAsync(string naturalLanguageInput, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/parse-alert", new { input = naturalLanguageInput }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ParseAlertApiResponse>(cancellationToken: cancellationToken);
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
}
