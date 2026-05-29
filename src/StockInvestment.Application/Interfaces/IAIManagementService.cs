namespace StockInvestment.Application.Interfaces;

public interface IAIManagementService
{
    Task<List<AiProviderDto>> GetProvidersAsync(CancellationToken cancellationToken = default);
    Task<AiProbeResult> ProbeProvidersAsync(CancellationToken cancellationToken = default);
    Task<AiPipelineDto> GetPipelineAsync(CancellationToken cancellationToken = default);
    Task<List<AiRagDocumentDto>> GetRagDocumentsAsync(CancellationToken cancellationToken = default);
    Task DeleteRagDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task<AiCacheStatsDto> GetCacheStatsAsync(CancellationToken cancellationToken = default);
    Task UpdateCacheTtlAsync(AiCacheTtlUpdateDto dto, CancellationToken cancellationToken = default);
    Task<List<AiJobDto>> GetJobsAsync(CancellationToken cancellationToken = default);
    Task RetryJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task CancelJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<AiParametersDto> GetParametersAsync(CancellationToken cancellationToken = default);
    Task UpdateParametersAsync(AiParametersUpdateDto dto, CancellationToken cancellationToken = default);
    Task<List<AiTraceDto>> GetTracesAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task ClearTracesAsync(CancellationToken cancellationToken = default);
}

public sealed class AiProviderDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int LatencyMs { get; set; }
    public int QuotaRemaining { get; set; } = -1;
    public int QuotaTotal { get; set; } = -1;
    public string KeyMasked { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? Note { get; set; }
}

public sealed class AiProbeResult
{
    public string ProbedAt { get; set; } = string.Empty;
    public List<AiProbeItem> Results { get; set; } = new();
}

public sealed class AiProbeItem
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int LatencyMs { get; set; }
    public string? Error { get; set; }
}

public sealed class AiPipelineDto
{
    public string Provider { get; set; } = string.Empty;
    public List<AiPipelineNode> Nodes { get; set; } = new();
    public List<AiPipelineEdge> Edges { get; set; } = new();
    public int EstimatedLlmCalls { get; set; }
}

public sealed class AiPipelineNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class AiPipelineEdge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public sealed class AiRagDocumentDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Chunks { get; set; }
    public int SizeBytes { get; set; }
    public string? IngestedAt { get; set; }
}

public sealed class AiCacheStatsDto
{
    public double HitRatePercent { get; set; }
    public double MemoryUsedMb { get; set; }
    public int TotalKeys { get; set; }
    public int ConnectedClients { get; set; }
    public int DbSize { get; set; }
}

public sealed class AiCacheTtlUpdateDto
{
    public int? AnalyzeTtl { get; set; }
    public int? QuoteTtl { get; set; }
    public int? HistoryTtl { get; set; }
    public int? SymbolsTtl { get; set; }
}

public sealed class AiJobDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? Provider { get; set; }
    public string? EnqueuedAt { get; set; }
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? Error { get; set; }
}

public sealed class AiParametersDto
{
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public string PromptVersion { get; set; } = string.Empty;
    public bool ShadowMode { get; set; }
    public double CanaryRatio { get; set; }
    public string LlmProvider { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
}

public sealed class AiParametersUpdateDto
{
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string? PromptVersion { get; set; }
    public bool? ShadowMode { get; set; }
    public double? CanaryRatio { get; set; }
}

public sealed class AiTraceDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string StartedAt { get; set; } = string.Empty;
    public int TotalMs { get; set; }
    public List<AiTraceNode> Nodes { get; set; } = new();
    public Dictionary<string, object> Result { get; set; } = new();
}

public sealed class AiTraceNode
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Ms { get; set; }
    public bool Parallel { get; set; }
}
