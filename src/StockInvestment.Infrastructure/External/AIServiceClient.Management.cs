using System.Net.Http.Json;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.External;

public partial class AIServiceClient : IAIManagementService
{
    private static string GetInternalApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable("AI_SERVICE_INTERNAL_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "AI_SERVICE_INTERNAL_API_KEY environment variable is not configured. " +
                "Set it to the internal API key shared with the AI service.");
        }
        return apiKey;
    }

    private HttpRequestMessage CreateMgmtRequest(HttpMethod method, string endpoint, object? body = null)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Add("X-Internal-Api-Key", GetInternalApiKey());
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }

    public async Task<List<AiProviderDto>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Get, "/api/manage/providers");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AiProviderDto>>(cancellationToken: cancellationToken);
        return result ?? new List<AiProviderDto>();
    }

    public async Task<AiProbeResult> ProbeProvidersAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Post, "/api/manage/providers/probe");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AiProbeResult>(cancellationToken: cancellationToken);
        return result ?? new AiProbeResult();
    }

    public async Task<AiPipelineDto> GetPipelineAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Get, "/api/manage/pipeline");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AiPipelineDto>(cancellationToken: cancellationToken);
        return result ?? new AiPipelineDto();
    }

    public async Task<List<AiRagDocumentDto>> GetRagDocumentsAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Get, "/api/manage/rag/documents");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AiRagDocumentDto>>(cancellationToken: cancellationToken);
        return result ?? new List<AiRagDocumentDto>();
    }

    public async Task DeleteRagDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Delete, $"/api/manage/rag/documents/{Uri.EscapeDataString(documentId)}");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AiCacheStatsDto> GetCacheStatsAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Get, "/api/manage/cache/stats");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AiCacheStatsDto>(cancellationToken: cancellationToken);
        return result ?? new AiCacheStatsDto();
    }

    public async Task UpdateCacheTtlAsync(AiCacheTtlUpdateDto dto, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Put, "/api/manage/cache/ttl", dto);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<AiJobDto>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Get, "/api/manage/jobs");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AiJobDto>>(cancellationToken: cancellationToken);
        return result ?? new List<AiJobDto>();
    }

    public async Task RetryJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Post, $"/api/manage/jobs/{Uri.EscapeDataString(jobId)}/retry");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Delete, $"/api/manage/jobs/{Uri.EscapeDataString(jobId)}");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AiParametersDto> GetParametersAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Get, "/api/manage/parameters");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AiParametersDto>(cancellationToken: cancellationToken);
        return result ?? new AiParametersDto();
    }

    public async Task UpdateParametersAsync(AiParametersUpdateDto dto, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Put, "/api/manage/parameters", dto);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<AiTraceDto>> GetTracesAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Get, $"/api/manage/traces?limit={limit}");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AiTraceDto>>(cancellationToken: cancellationToken);
        return result ?? new List<AiTraceDto>();
    }

    public async Task ClearTracesAsync(CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = CreateMgmtRequest(HttpMethod.Delete, "/api/manage/traces");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
