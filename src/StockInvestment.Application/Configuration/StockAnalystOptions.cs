namespace StockInvestment.Application.Configuration;

/// <summary>
/// LangGraph Stock Analyst Python service (<c>POST /api/analyze</c>).
/// </summary>
public class StockAnalystOptions
{
    public const string SectionName = "StockAnalyst";

    /// <summary>When true, forecast dashboard uses LangGraph instead of classic ai-service forecast endpoints.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL of the Python <c>ai</c> app. When empty, HttpClient setup falls back to AIService:BaseUrl.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP timeout for multi-node graph (default 300s).</summary>
    public int TimeoutSeconds { get; set; } = 300;
}
