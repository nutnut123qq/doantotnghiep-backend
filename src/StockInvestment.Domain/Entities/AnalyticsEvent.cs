namespace StockInvestment.Domain.Entities;

/// <summary>
/// Analytics event for tracking system usage
/// </summary>
public class AnalyticsEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty; // ApiRequest, StockView, UserActivity
    public string? Endpoint { get; set; }
    public string? Method { get; set; }
    public int? StatusCode { get; set; }
    public long? ResponseTimeMs { get; set; }
    public string? Symbol { get; set; }
    public string? UserId { get; set; }
    public string? ActivityType { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
