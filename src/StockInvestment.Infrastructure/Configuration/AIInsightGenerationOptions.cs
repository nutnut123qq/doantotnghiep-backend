namespace StockInvestment.Infrastructure.Configuration;

public class AIInsightGenerationOptions
{
    public const string SectionName = "AIInsights:Generation";

    /// <summary>When false, the hosted insight job idles (no outbound AI insight generation).</summary>
    public bool Enabled { get; set; } = true;

    // Steady-state profile (balanced, 2-4h freshness)
    public int IntervalMinutes { get; set; } = 120;
    public int StartupDelayMinutes { get; set; } = 5;
    public int ScheduledTopSymbols { get; set; } = 30;
    public int MaxGeneratePerRun { get; set; } = 10;
    public int MinInsightTtlMinutes { get; set; } = 180;
    public decimal TriggerChangePercent { get; set; } = 2.8m;
    public int TriggerNewsLookbackMinutes { get; set; } = 90;

    // Warm-up profile (backfill to reach full coverage quickly)
    public bool EnableWarmupProfile { get; set; } = true;
    public int WarmupIntervalMinutes { get; set; } = 60;
    public int WarmupMaxGeneratePerRun { get; set; } = 15;
    public int WarmupMinInsightTtlMinutes { get; set; } = 60;
}
