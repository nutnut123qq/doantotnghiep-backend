using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

/// <summary>
/// Dashboard controller providing stats, performance, and top performers
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IPortfolioService portfolioService,
        ILogger<DashboardController> logger)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var summary = await _portfolioService.GetSummaryAsync(userId);
            var holdings = await _portfolioService.GetHoldingsAsync(userId);
            var holdingsList = holdings.ToList();

            // Calculate today change (simplified - using current price vs avg price as proxy)
            // In a real implementation, you'd track historical prices
            decimal todayChange = 0;
            decimal todayChangePercentage = 0;
            if (summary.TotalValue > 0 && summary.TotalCost > 0)
            {
                // Use gain/loss as today change approximation
                todayChange = summary.TotalGainLoss;
                todayChangePercentage = summary.TotalGainLossPercentage;
            }

            var stats = new DashboardStatsDto
            {
                PortfolioValue = summary.TotalValue,
                TotalGainLoss = summary.TotalGainLoss,
                TotalGainLossPercentage = summary.TotalGainLossPercentage,
                TodayChange = todayChange,
                TodayChangePercentage = todayChangePercentage,
                ActivePositions = holdingsList.Count(h => h.Shares > 0)
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving dashboard stats");
        }
    }

    /// <summary>
    /// Get performance data over time
    /// </summary>
    [HttpGet("performance")]
    public async Task<IActionResult> GetPerformance([FromQuery] string period = "1M")
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var summary = await _portfolioService.GetSummaryAsync(userId);
            
            // Simplified implementation - return current portfolio value for all dates
            // In a real implementation, you'd track historical portfolio values
            var days = period switch
            {
                "1W" => 7,
                "1M" => 30,
                "3M" => 90,
                "6M" => 180,
                "1Y" => 365,
                _ => 30
            };

            var performanceData = new List<PerformanceDataDto>();
            var currentDate = DateTime.UtcNow.Date;
            
            for (int i = days - 1; i >= 0; i--)
            {
                var date = currentDate.AddDays(-i);
                performanceData.Add(new PerformanceDataDto
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    Value = (double)summary.TotalValue // Simplified - same value for all dates
                });
            }

            return Ok(performanceData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance data for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving performance data");
        }
    }

    /// <summary>
    /// Get top performing stocks in portfolio
    /// </summary>
    [HttpGet("top-performers")]
    public async Task<IActionResult> GetTopPerformers([FromQuery] int limit = 5)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var holdings = await _portfolioService.GetHoldingsAsync(userId);
            var holdingsList = holdings.ToList();

            var topPerformers = holdingsList
                .Where(h => h.Shares > 0)
                .OrderByDescending(h => h.GainLossPercentage)
                .Take(limit)
                .Select(h => new TopPerformerDto
                {
                    Symbol = h.Symbol,
                    Name = h.Name,
                    Change = (double)h.GainLoss,
                    ChangePercentage = (double)h.GainLossPercentage
                })
                .ToList();

            return Ok(topPerformers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top performers for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving top performers");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

public class DashboardStatsDto
{
    public decimal PortfolioValue { get; set; }
    public decimal TotalGainLoss { get; set; }
    public decimal TotalGainLossPercentage { get; set; }
    public decimal TodayChange { get; set; }
    public decimal TodayChangePercentage { get; set; }
    public int ActivePositions { get; set; }
}

public class PerformanceDataDto
{
    public string Date { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class TopPerformerDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Change { get; set; }
    public double ChangePercentage { get; set; }
}
