namespace StockInvestment.Infrastructure.Configuration;

/// <summary>
/// RSS ingestion for corporate events (maps announcement-style feeds to corporate event rows).
/// Reuses <see cref="NewsSourceConfig"/> shape; phase 1 supports <c>Kind: Rss</c> only.
/// </summary>
public sealed class EventIngestionOptions
{
    public const string SectionName = "EventIngestion";

    public bool Enabled { get; set; } = true;

    public int InitialDelaySeconds { get; set; } = 45;

    public int PollMinutes { get; set; } = 15;

    public int MaxItemsPerRun { get; set; } = 40;

    public List<NewsSourceConfig> Sources { get; set; } = new();
}
