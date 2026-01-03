namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Service for tracking and analyzing system metrics
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Track an API request
    /// </summary>
    Task TrackApiRequestAsync(string endpoint, string method, int statusCode, long responseTimeMs, string? userId = null);
    
    /// <summary>
    /// Track stock view
    /// </summary>
    Task TrackStockViewAsync(string symbol, string userId);
    
    /// <summary>
    /// Track user activity
    /// </summary>
    Task TrackUserActivityAsync(string userId, string activityType, string? metadata = null);
    
    /// <summary>
    /// Get API analytics
    /// </summary>
    Task<ApiAnalytics> GetApiAnalyticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    
    /// <summary>
    /// Get popular stocks
    /// </summary>
    Task<List<PopularStock>> GetPopularStocksAsync(int topN = 10, int daysBack = 7);
    
    /// <summary>
    /// Get user activity summary
    /// </summary>
    Task<UserActivitySummary> GetUserActivitySummaryAsync(int daysBack = 7);
    
    /// <summary>
    /// Get endpoint performance metrics
    /// </summary>
    Task<List<EndpointMetrics>> GetEndpointMetricsAsync(int topN = 20);
}

/// <summary>
/// API analytics data
/// </summary>
public class ApiAnalytics
{
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double ErrorRate { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public Dictionary<string, long> RequestsByEndpoint { get; set; } = new();
    public Dictionary<int, long> RequestsByStatusCode { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Popular stock data
/// </summary>
public class PopularStock
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public int UniqueUsers { get; set; }
}

/// <summary>
/// User activity summary
/// </summary>
public class UserActivitySummary
{
    public long TotalActivities { get; set; }
    public int ActiveUsers { get; set; }
    public Dictionary<string, long> ActivitiesByType { get; set; } = new();
    public List<DailyActivity> DailyActivities { get; set; } = new();
}

public class DailyActivity
{
    public DateTime Date { get; set; }
    public long ActivityCount { get; set; }
    public int UniqueUsers { get; set; }
}

/// <summary>
/// Endpoint performance metrics
/// </summary>
public class EndpointMetrics
{
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double P95ResponseTimeMs { get; set; }
    public double P99ResponseTimeMs { get; set; }
    public long ErrorCount { get; set; }
    public double ErrorRate { get; set; }
}
