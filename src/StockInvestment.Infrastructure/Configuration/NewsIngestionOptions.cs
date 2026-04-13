namespace StockInvestment.Infrastructure.Configuration;

public sealed class NewsIngestionOptions
{
    public const string SectionName = "NewsIngestion";

    /// <summary>Delay before the first crawl after startup.</summary>
    public int InitialDelaySeconds { get; set; } = 10;

    /// <summary>Interval between crawl runs.</summary>
    public int PollMinutes { get; set; } = 10;

    /// <summary>Max items kept after merging all sources (dedupe by URL happens in the job).</summary>
    public int MaxArticlesPerRun { get; set; } = 80;

    /// <summary>
    /// If a source returns fewer than this count, fallback sources are attempted.
    /// </summary>
    public int MinItemsBeforeFallback { get; set; } = 5;

    /// <summary>
    /// When empty, the crawler falls back to the legacy CafeF + VNExpress + VietStock HTML paths.
    /// </summary>
    public List<NewsSourceConfig> Sources { get; set; } = new();

    /// <summary>
    /// Path segments (URL slugs) that disqualify an article when any segment of the URL path matches (case-insensitive).
    /// When null or empty, no URL-based filtering is applied.
    /// </summary>
    public List<string> BlockedUrlPathSegments { get; set; } = new();
}

/// <summary>
/// One ingest source. Kind: <c>Rss</c>, <c>HtmlBuiltin</c>, or <c>HtmlGeneric</c> (case-insensitive).
/// </summary>
public sealed class NewsSourceConfig
{
    public string Name { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public string Kind { get; set; } = "HtmlBuiltin";

    public string? Url { get; set; }

    public int? MaxItems { get; set; }

    /// <summary>For HtmlBuiltin: CafeF, VNExpress, VietStock.</summary>
    public string? HtmlTemplate { get; set; }

    public string? ItemXPath { get; set; }
    public string? TitleXPath { get; set; }
    public string? LinkXPath { get; set; }
    public string? SummaryXPath { get; set; }
    public string? TimeXPath { get; set; }

    /// <summary>Prefix for relative links in HtmlGeneric mode.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Lower value means higher priority in execution order.
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Minimum number of items expected from this source before trying fallback sources.
    /// If null, the pipeline-level threshold is used.
    /// </summary>
    public int? MinItemsBeforeFallback { get; set; }

    /// <summary>
    /// Secondary sources to be attempted when this source is unhealthy or returns too few items.
    /// </summary>
    public List<NewsSourceConfig> FallbackSources { get; set; } = new();
}
