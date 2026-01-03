using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Attributes;
using StockInvestment.Application.Features.Admin.GetAllUsers;
using StockInvestment.Application.Features.Admin.GetSystemStats;
using StockInvestment.Application.Features.Admin.GetSystemHealth;
using StockInvestment.Application.Features.Admin.UpdateUserRole;
using StockInvestment.Application.Features.Admin.SetUserActiveStatus;
using StockInvestment.Application.Features.Admin.GetAnalytics;
using StockInvestment.Application.Features.Admin.GetPopularStocks;
using StockInvestment.Application.Features.Admin.GetEndpointMetrics;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[AdminOnly]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IMediator mediator, ILogger<AdminController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all users with pagination (Admin only)
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = new GetAllUsersQuery { Page = page, PageSize = pageSize };
            var result = await _mediator.Send(query);
            
            return Ok(new
            {
                users = result.Users,
                totalCount = result.TotalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            return StatusCode(500, "An error occurred while retrieving users");
        }
    }

    /// <summary>
    /// Get system statistics (Admin only)
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult> GetSystemStats()
    {
        try
        {
            var query = new GetSystemStatsQuery();
            var stats = await _mediator.Send(query);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system stats");
            return StatusCode(500, "An error occurred while retrieving statistics");
        }
    }

    /// <summary>
    /// Get system health status (Admin only)
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult> GetSystemHealth()
    {
        try
        {
            var query = new GetSystemHealthQuery();
            var health = await _mediator.Send(query);
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system health");
            return StatusCode(500, "An error occurred while checking system health");
        }
    }

    /// <summary>
    /// Get API analytics (Admin only)
    /// </summary>
    [HttpGet("analytics")]
    public async Task<ActionResult> GetAnalytics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var query = new GetAnalyticsQuery { StartDate = startDate, EndDate = endDate };
            var analytics = await _mediator.Send(query);
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analytics");
            return StatusCode(500, "An error occurred while retrieving analytics");
        }
    }

    /// <summary>
    /// Get popular stocks (Admin only)
    /// </summary>
    [HttpGet("popular-stocks")]
    public async Task<ActionResult> GetPopularStocks([FromQuery] int topN = 10, [FromQuery] int daysBack = 7)
    {
        try
        {
            var query = new GetPopularStocksQuery { TopN = topN, DaysBack = daysBack };
            var stocks = await _mediator.Send(query);
            return Ok(stocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular stocks");
            return StatusCode(500, "An error occurred while retrieving popular stocks");
        }
    }

    /// <summary>
    /// Get endpoint performance metrics (Admin only)
    /// </summary>
    [HttpGet("endpoint-metrics")]
    public async Task<ActionResult> GetEndpointMetrics([FromQuery] int topN = 20)
    {
        try
        {
            var query = new GetEndpointMetricsQuery { TopN = topN };
            var metrics = await _mediator.Send(query);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting endpoint metrics");
            return StatusCode(500, "An error occurred while retrieving endpoint metrics");
        }
    }

    /// <summary>
    /// Update user role (Admin only)
    /// </summary>
    [HttpPut("users/{userId}/role")]
    public async Task<ActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request)
    {
        try
        {
            var command = new UpdateUserRoleCommand
            {
                UserId = userId,
                NewRole = request.NewRole
            };

            var success = await _mediator.Send(command);
            
            if (!success)
            {
                return NotFound($"User with ID {userId} not found");
            }

            return Ok(new { message = "User role updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role for user {UserId}", userId);
            return StatusCode(500, "An error occurred while updating user role");
        }
    }

    /// <summary>
    /// Activate or deactivate user (Admin only)
    /// </summary>
    [HttpPut("users/{userId}/status")]
    public async Task<ActionResult> SetUserActiveStatus(Guid userId, [FromBody] SetUserStatusRequest request)
    {
        try
        {
            var command = new SetUserActiveStatusCommand
            {
                UserId = userId,
                IsActive = request.IsActive
            };

            var success = await _mediator.Send(command);
            
            if (!success)
            {
                return NotFound($"User with ID {userId} not found");
            }

            var action = request.IsActive ? "activated" : "deactivated";
            return Ok(new { message = $"User {action} successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting user status for user {UserId}", userId);
            return StatusCode(500, "An error occurred while updating user status");
        }
    }
}

/// <summary>
/// Request model for updating user role
/// </summary>
public class UpdateUserRoleRequest
{
    public UserRole NewRole { get; set; }
}

/// <summary>
/// Request model for setting user status
/// </summary>
public class SetUserStatusRequest
{
    public bool IsActive { get; set; }
}
