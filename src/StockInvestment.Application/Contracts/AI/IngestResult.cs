using System.Text.Json.Serialization;

namespace StockInvestment.Application.Contracts.AI;

/// <summary>
/// Result from RAG ingest operation
/// </summary>
public class IngestResult
{
    [JsonPropertyName("chunksUpserted")]
    public int ChunksUpserted { get; set; }
    
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;
    
    [JsonPropertyName("collection")]
    public string Collection { get; set; } = string.Empty;
    
    [JsonPropertyName("embeddingModel")]
    public string EmbeddingModel { get; set; } = string.Empty;
}
