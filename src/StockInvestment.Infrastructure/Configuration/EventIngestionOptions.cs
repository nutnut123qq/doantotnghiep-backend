namespace StockInvestment.Infrastructure.Configuration;

/// <summary>
/// Ingestion for corporate events (RSS first, optional HTML fallback).
/// Reuses <see cref="NewsSourceConfig"/> shape for source and fallback definitions.
/// </summary>
public sealed class EventIngestionOptions
{
    public const string SectionName = "EventIngestion";

    public bool Enabled { get; set; } = true;

    public int InitialDelaySeconds { get; set; } = 45;

    public int PollMinutes { get; set; } = 15;

    public int MaxItemsPerRun { get; set; } = 40;

    /// <summary>
    /// If a source yields fewer than this count, fallback sources are attempted.
    /// </summary>
    public int MinItemsBeforeFallback { get; set; } = 2;

    public List<NewsSourceConfig> Sources { get; set; } = new();
}
