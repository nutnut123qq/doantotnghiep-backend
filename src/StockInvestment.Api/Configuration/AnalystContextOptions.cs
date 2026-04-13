namespace StockInvestment.Api.Configuration;

public class AnalystContextOptions
{
    public const string SectionName = "AnalystContext";

    /// <summary>
    /// When set, requests must include header X-Internal-Api-Key with this value.
    /// When empty, endpoints are open (development convenience; avoid in production).
    /// </summary>
    public string? ApiKey { get; set; }
}
