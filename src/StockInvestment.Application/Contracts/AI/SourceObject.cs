using System.Text.Json.Serialization;

namespace StockInvestment.Application.Contracts.AI;

/// <summary>
/// Source object from RAG retrieval with full metadata
/// </summary>
public class SourceObject
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;
    
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
    
    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;
    
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
    
    [JsonPropertyName("chunkId")]
    public string ChunkId { get; set; } = string.Empty;
    
    [JsonPropertyName("score")]
    public double Score { get; set; }
    
    [JsonPropertyName("textPreview")]
    public string TextPreview { get; set; } = string.Empty;
}
