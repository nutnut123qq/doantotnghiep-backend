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
}
