namespace StockInvestment.Infrastructure.Configuration;

/// <summary>
/// Options for scheduled financial report crawling (similar to <see cref="NewsIngestionOptions"/>).
/// </summary>
public sealed class FinancialIngestionOptions
{
    public const string SectionName = "FinancialIngestion";

    /// <summary>When false, the background job does not run.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Delay before the first crawl after startup.</summary>
    public int InitialDelaySeconds { get; set; } = 30;

    /// <summary>Interval between crawl runs.</summary>
    public int PollMinutes { get; set; } = 10;

    /// <summary>How many tickers from StockTickers to process per run (ordered by LastUpdated desc).</summary>
    public int TopTickersPerRun { get; set; } = 15;

    /// <summary>Max reports to request from crawler per symbol.</summary>
    public int MaxReportsPerSymbol { get; set; } = 10;

    /// <summary>
    /// Symbols merged ahead of the DB top-N list each run (e.g. dashboard favorites like VIC).
    /// Only symbols that exist in <c>StockTickers</c> are persisted.
    /// </summary>
    public string[] AdditionalSymbols { get; set; } = [];
}
