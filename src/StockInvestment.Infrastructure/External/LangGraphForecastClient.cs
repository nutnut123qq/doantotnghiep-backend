using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.LangGraph;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.External;

public sealed class LangGraphForecastClient : ILangGraphForecastClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions PostJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LangGraphForecastClient> _logger;

    public LangGraphForecastClient(HttpClient httpClient, ILogger<LangGraphForecastClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LangGraphAnalyzeResponse?> AnalyzeAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        var sym = symbol.Trim().ToUpperInvariant();
        var body = new AnalyzeRequestBody(sym, string.Empty, string.Empty);

        using var response = await _httpClient.PostAsJsonAsync("/api/analyze", body, PostJsonOptions, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "LangGraph StockAnalyst returned {StatusCode} for {Symbol}: {Body}",
                (int)response.StatusCode,
                sym,
                payload.Length > 500 ? payload[..500] + "…" : payload);

            var ex = new HttpRequestException($"StockAnalyst returned {(int)response.StatusCode}: {payload}")
            {
                Data = { ["StatusCode"] = (int)response.StatusCode }
            };
            throw ex;
        }

        try
        {
            return JsonSerializer.Deserialize<LangGraphAnalyzeResponse>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize LangGraph response for {Symbol}", sym);
            throw;
        }
    }

    public async Task<LangGraphJobResponse?> EnqueueAnalyzeAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        var sym = symbol.Trim().ToUpperInvariant();
        var body = new AnalyzeRequestBody(sym, string.Empty, string.Empty);

        using var response = await _httpClient.PostAsJsonAsync("/api/analyze/enqueue", body, PostJsonOptions, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        // Python returns 200 on cache hit and 202 on queued — both are success shapes.
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Accepted)
        {
            _logger.LogWarning(
                "LangGraph enqueue returned {StatusCode} for {Symbol}: {Body}",
                (int)response.StatusCode,
                sym,
                payload.Length > 500 ? payload[..500] + "…" : payload);

            var ex = new HttpRequestException($"StockAnalyst enqueue returned {(int)response.StatusCode}: {payload}")
            {
                Data = { ["StatusCode"] = (int)response.StatusCode }
            };
            throw ex;
        }

        try
        {
            return JsonSerializer.Deserialize<LangGraphJobResponse>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize LangGraph enqueue response for {Symbol}", sym);
            throw;
        }
    }

    public async Task<LangGraphJobResponse?> GetAnalyzeJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("JobId is required", nameof(jobId));

        using var response = await _httpClient.GetAsync($"/api/analyze/jobs/{Uri.EscapeDataString(jobId)}", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("LangGraph job {JobId} not found (404 from Python)", jobId);
            return null;
        }

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Accepted)
        {
            _logger.LogWarning(
                "LangGraph job status returned {StatusCode} for {JobId}: {Body}",
                (int)response.StatusCode,
                jobId,
                payload.Length > 500 ? payload[..500] + "…" : payload);

            var ex = new HttpRequestException($"StockAnalyst job status returned {(int)response.StatusCode}: {payload}")
            {
                Data = { ["StatusCode"] = (int)response.StatusCode }
            };
            throw ex;
        }

        try
        {
            return JsonSerializer.Deserialize<LangGraphJobResponse>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize LangGraph job response for {JobId}", jobId);
            throw;
        }
    }

    private sealed record AnalyzeRequestBody(string Symbol, string NewsContext, string TechContext);
}
