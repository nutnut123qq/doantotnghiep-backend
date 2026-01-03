using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.Services;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Service for tracking and analyzing system metrics
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly ILogger<AnalyticsService> _logger;
    private const string CacheKeyPrefix = "analytics:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public AnalyticsService(
        ApplicationDbContext dbContext,
        ICacheService cache,
        ILogger<AnalyticsService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task TrackApiRequestAsync(string endpoint, string method, int statusCode, long responseTimeMs, string? userId = null)
    {
        try
        {
            var analyticsEvent = new AnalyticsEvent
            {
                EventType = "ApiRequest",
                Endpoint = endpoint,
                Method = method,
                StatusCode = statusCode,
                ResponseTimeMs = responseTimeMs,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Set<AnalyticsEvent>().Add(analyticsEvent);
            await _dbContext.SaveChangesAsync();
            
            // Invalidate cache
            await InvalidateAnalyticsCacheAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking API request for {Endpoint}", endpoint);
        }
    }

    public async Task TrackStockViewAsync(string symbol, string userId)
    {
        try
        {
            var analyticsEvent = new AnalyticsEvent
            {
                EventType = "StockView",
                Symbol = symbol,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Set<AnalyticsEvent>().Add(analyticsEvent);
            await _dbContext.SaveChangesAsync();
            
            // Invalidate popular stocks cache
            await _cache.RemoveAsync($"{CacheKeyPrefix}popular_stocks").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking stock view for {Symbol}", symbol);
        }
    }

    public async Task TrackUserActivityAsync(string userId, string activityType, string? metadata = null)
    {
        try
        {
            var analyticsEvent = new AnalyticsEvent
            {
                EventType = "UserActivity",
                UserId = userId,
                ActivityType = activityType,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Set<AnalyticsEvent>().Add(analyticsEvent);
            await _dbContext.SaveChangesAsync();
            
            // Invalidate activity cache
            await _cache.RemoveAsync($"{CacheKeyPrefix}user_activity").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking user activity for {UserId}", userId);
        }
    }

    public async Task<ApiAnalytics> GetApiAnalyticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-7);
        var end = endDate ?? DateTime.UtcNow;
        
        var cacheKey = $"{CacheKeyPrefix}api_analytics:{start:yyyyMMdd}:{end:yyyyMMdd}";
        var cached = await _cache.GetAsync<ApiAnalytics>(cacheKey);
        
        if (cached != null)
        {
            return cached;
        }

        var events = await _dbContext.Set<AnalyticsEvent>()
            .Where(e => e.EventType == "ApiRequest" && e.CreatedAt >= start && e.CreatedAt <= end)
            .AsNoTracking()
            .ToListAsync();

        var analytics = new ApiAnalytics
        {
            TotalRequests = events.Count,
            SuccessfulRequests = events.Count(e => e.StatusCode >= 200 && e.StatusCode < 300),
            FailedRequests = events.Count(e => e.StatusCode >= 400),
            AverageResponseTimeMs = events.Any() ? events.Average(e => e.ResponseTimeMs ?? 0) : 0,
            RequestsByEndpoint = events
                .GroupBy(e => e.Endpoint ?? "Unknown")
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            RequestsByStatusCode = events
                .Where(e => e.StatusCode.HasValue)
                .GroupBy(e => e.StatusCode!.Value)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            StartDate = start,
            EndDate = end
        };

        analytics.ErrorRate = analytics.TotalRequests > 0 
            ? (double)analytics.FailedRequests / analytics.TotalRequests * 100 
            : 0;

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, analytics, CacheDuration);

        return analytics;
    }

    public async Task<List<PopularStock>> GetPopularStocksAsync(int topN = 10, int daysBack = 7)
    {
        var cacheKey = $"{CacheKeyPrefix}popular_stocks:{topN}:{daysBack}";
        var cached = await _cache.GetAsync<List<PopularStock>>(cacheKey);
        
        if (cached != null)
        {
            return cached;
        }

        var startDate = DateTime.UtcNow.AddDays(-daysBack);

        var popularStocks = await _dbContext.Set<AnalyticsEvent>()
            .Where(e => e.EventType == "StockView" && e.CreatedAt >= startDate && e.Symbol != null)
            .GroupBy(e => e.Symbol)
            .Select(g => new PopularStock
            {
                Symbol = g.Key!,
                ViewCount = g.Count(),
                UniqueUsers = g.Select(e => e.UserId).Distinct().Count()
            })
            .OrderByDescending(s => s.ViewCount)
            .Take(topN)
            .ToListAsync();

        // Get company names from StockTicker table
        var symbols = popularStocks.Select(s => s.Symbol).ToList();
        var stocks = await _dbContext.Set<StockTicker>()
            .Where(s => symbols.Contains(s.Symbol))
            .AsNoTracking()
            .ToListAsync();

        foreach (var popular in popularStocks)
        {
            var stock = stocks.FirstOrDefault(s => s.Symbol == popular.Symbol);
            if (stock != null)
            {
                popular.CompanyName = stock.Symbol; // Use Symbol as company name for now
            }
        }

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, popularStocks, CacheDuration);

        return popularStocks;
    }

    public async Task<UserActivitySummary> GetUserActivitySummaryAsync(int daysBack = 7)
    {
        var cacheKey = $"{CacheKeyPrefix}user_activity:{daysBack}";
        var cached = await _cache.GetAsync<UserActivitySummary>(cacheKey);
        
        if (cached != null)
        {
            return cached;
        }

        var startDate = DateTime.UtcNow.AddDays(-daysBack);

        var events = await _dbContext.Set<AnalyticsEvent>()
            .Where(e => e.EventType == "UserActivity" && e.CreatedAt >= startDate)
            .AsNoTracking()
            .ToListAsync();

        var summary = new UserActivitySummary
        {
            TotalActivities = events.Count,
            ActiveUsers = events.Select(e => e.UserId).Distinct().Count(),
            ActivitiesByType = events
                .GroupBy(e => e.ActivityType ?? "Unknown")
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            DailyActivities = events
                .GroupBy(e => e.CreatedAt.Date)
                .Select(g => new DailyActivity
                {
                    Date = g.Key,
                    ActivityCount = g.Count(),
                    UniqueUsers = g.Select(e => e.UserId).Distinct().Count()
                })
                .OrderBy(d => d.Date)
                .ToList()
        };

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, summary, CacheDuration);

        return summary;
    }

    public async Task<List<EndpointMetrics>> GetEndpointMetricsAsync(int topN = 20)
    {
        var cacheKey = $"{CacheKeyPrefix}endpoint_metrics:{topN}";
        var cached = await _cache.GetAsync<List<EndpointMetrics>>(cacheKey);
        
        if (cached != null)
        {
            return cached;
        }

        var startDate = DateTime.UtcNow.AddDays(-1); // Last 24 hours

        var events = await _dbContext.Set<AnalyticsEvent>()
            .Where(e => e.EventType == "ApiRequest" && e.CreatedAt >= startDate && e.Endpoint != null)
            .AsNoTracking()
            .ToListAsync();

        var metrics = events
            .GroupBy(e => new { e.Endpoint, e.Method })
            .Select(g =>
            {
                var responseTimes = g.Where(e => e.ResponseTimeMs.HasValue)
                    .Select(e => e.ResponseTimeMs!.Value)
                    .OrderBy(t => t)
                    .ToList();

                var p95Index = (int)(responseTimes.Count * 0.95);
                var p99Index = (int)(responseTimes.Count * 0.99);

                return new EndpointMetrics
                {
                    Endpoint = g.Key.Endpoint!,
                    Method = g.Key.Method ?? "GET",
                    RequestCount = g.Count(),
                    AverageResponseTimeMs = responseTimes.Any() ? responseTimes.Average() : 0,
                    P95ResponseTimeMs = responseTimes.Count > p95Index ? responseTimes[p95Index] : 0,
                    P99ResponseTimeMs = responseTimes.Count > p99Index ? responseTimes[p99Index] : 0,
                    ErrorCount = g.Count(e => e.StatusCode >= 400),
                    ErrorRate = g.Count() > 0 ? (double)g.Count(e => e.StatusCode >= 400) / g.Count() * 100 : 0
                };
            })
            .OrderByDescending(m => m.RequestCount)
            .Take(topN)
            .ToList();

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, metrics, CacheDuration);

        return metrics;
    }

    private async Task InvalidateAnalyticsCacheAsync()
    {
        try
        {
            await _cache.RemoveAsync($"{CacheKeyPrefix}api_analytics");
            await _cache.RemoveAsync($"{CacheKeyPrefix}endpoint_metrics");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating analytics cache");
        }
    }
}
