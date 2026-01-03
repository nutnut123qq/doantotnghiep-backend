namespace StockInvestment.Domain.Entities;

public class AIModelPerformance
{
    public Guid Id { get; set; }
    public Guid ModelConfigId { get; set; }
    public string FeatureType { get; set; } = string.Empty; // "Forecast", "Summarize", "Q&A"
    public double Accuracy { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime RecordedAt { get; set; }
}

