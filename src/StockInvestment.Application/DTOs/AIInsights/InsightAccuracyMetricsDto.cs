namespace StockInvestment.Application.DTOs.AIInsights;

public class InsightAccuracyMetricsDto
{
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public int TotalInsightsConsidered { get; set; }
    public HorizonMetricDto TPlus1 { get; set; } = new();
    public HorizonMetricDto TPlus5 { get; set; } = new();
    public HorizonMetricDto TPlus20 { get; set; } = new();
    public decimal ConfidenceCalibrationError { get; set; }
}

public class HorizonMetricDto
{
    public int EligibleInsights { get; set; }
    public int CorrectPredictions { get; set; }
    public int FalseSignals { get; set; }
    public decimal HitRate => EligibleInsights == 0
        ? 0
        : Math.Round((decimal)CorrectPredictions / EligibleInsights * 100, 2);
}
