using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StockInvestment.Application.Features.Admin.CreateUser;
using StockInvestment.Application.Features.Admin.GetAllUsers;
using StockInvestment.Application.Features.Admin.GetSystemStats;
using StockInvestment.Application.Features.Admin.GetSystemHealth;
using StockInvestment.Application.Features.Admin.LockUser;
using StockInvestment.Application.Features.Admin.ResetUserPassword;
using StockInvestment.Application.Features.Admin.UpdateUserRole;
using StockInvestment.Application.Features.Admin.UpdateUser;
using StockInvestment.Application.Features.Admin.SetUserActiveStatus;
using StockInvestment.Application.Features.Admin.UnlockUser;
using StockInvestment.Application.Features.Admin.GetAnalytics;
using StockInvestment.Application.Features.Admin.GetPopularStocks;
using StockInvestment.Application.Features.Admin.GetEndpointMetrics;
using StockInvestment.Domain.Enums;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Linq.Expressions;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<AdminController> logger)
    {
        _mediator = mediator;
        _unitOfWork = unitOfWork;
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
    /// Create a new user (Admin only)
    /// </summary>
    [HttpPost("users")]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!TryGetAdminUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new CreateUserCommand
            {
                AdminUserId = adminUserId,
                Email = request.Email,
                Password = request.Password,
                Role = request.Role,
                FullName = request.FullName
            };

            var result = await _mediator.Send(command);
            if (!result.Success || result.Data == null)
            {
                return BadRequest(result.ErrorMessage ?? "Failed to create user");
            }

            return Ok(new { message = "User created successfully", user = result.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "An error occurred while creating user");
        }
    }

    /// <summary>
    /// Update user info/role (Admin only)
    /// </summary>
    [HttpPut("users/{userId}")]
    public async Task<ActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
    {
        if (!TryGetAdminUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new UpdateUserCommand
            {
                AdminUserId = adminUserId,
                UserId = userId,
                Email = request.Email,
                FullName = request.FullName,
                Role = request.Role
            };

            var result = await _mediator.Send(command);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage ?? "Failed to update user");
            }

            return Ok(new { message = "User updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return StatusCode(500, "An error occurred while updating user");
        }
    }

    /// <summary>
    /// Reset user password (Admin only)
    /// </summary>
    [HttpPost("users/{userId}/reset-password")]
    public async Task<ActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordRequest request)
    {
        if (!TryGetAdminUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new ResetUserPasswordCommand
            {
                AdminUserId = adminUserId,
                UserId = userId,
                NewPassword = request.NewPassword
            };

            var result = await _mediator.Send(command);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage ?? "Failed to reset password");
            }

            return Ok(new { message = "Password reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return StatusCode(500, "An error occurred while resetting password");
        }
    }

    /// <summary>
    /// Lock user account (Admin only)
    /// </summary>
    [HttpPost("users/{userId}/lock")]
    public async Task<ActionResult> LockUser(Guid userId)
    {
        if (!TryGetAdminUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new LockUserCommand
            {
                AdminUserId = adminUserId,
                UserId = userId
            };

            var result = await _mediator.Send(command);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage ?? "Failed to lock user");
            }

            return Ok(new { message = "User locked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking user {UserId}", userId);
            return StatusCode(500, "An error occurred while locking user");
        }
    }

    /// <summary>
    /// Unlock user account (Admin only)
    /// </summary>
    [HttpPost("users/{userId}/unlock")]
    public async Task<ActionResult> UnlockUser(Guid userId)
    {
        if (!TryGetAdminUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new UnlockUserCommand
            {
                AdminUserId = adminUserId,
                UserId = userId
            };

            var result = await _mediator.Send(command);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage ?? "Failed to unlock user");
            }

            return Ok(new { message = "User unlocked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking user {UserId}", userId);
            return StatusCode(500, "An error occurred while unlocking user");
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

    /// <summary>
    /// Get all shared layouts with pagination and filters (Admin only)
    /// </summary>
    [HttpGet("shared-layouts")]
    public async Task<ActionResult> GetSharedLayouts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? ownerId = null,
        [FromQuery] string status = "all")
    {
        try
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            Guid? ownerGuid = null;
            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                if (!Guid.TryParse(ownerId, out var parsed))
                {
                    return BadRequest("Invalid ownerId");
                }
                ownerGuid = parsed;
            }

            var now = DateTime.UtcNow;
            var normalizedStatus = status.ToLowerInvariant();
            Expression<Func<SharedLayout, bool>> predicate;

            if (ownerGuid.HasValue)
            {
                predicate = normalizedStatus switch
                {
                    "active" => sl => sl.OwnerId == ownerGuid.Value && sl.ExpiresAt > now,
                    "expired" => sl => sl.OwnerId == ownerGuid.Value && sl.ExpiresAt <= now,
                    _ => sl => sl.OwnerId == ownerGuid.Value
                };
            }
            else
            {
                predicate = normalizedStatus switch
                {
                    "active" => sl => sl.ExpiresAt > now,
                    "expired" => sl => sl.ExpiresAt <= now,
                    _ => sl => true
                };
            }

            var repository = _unitOfWork.Repository<SharedLayout>();
            var totalCount = await repository.CountAsync(predicate);
            var items = await repository.FindAsync(predicate);

            var pagedItems = items
                .OrderByDescending(sl => sl.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(sl => new
                {
                    id = sl.Id,
                    code = sl.Code,
                    ownerId = sl.OwnerId,
                    createdAt = sl.CreatedAt,
                    expiresAt = sl.ExpiresAt,
                    isPublic = sl.IsPublic,
                    isExpired = sl.ExpiresAt <= now
                });

            return Ok(new
            {
                items = pagedItems,
                totalCount,
                page,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared layouts");
            return StatusCode(500, "An error occurred while retrieving shared layouts");
        }
    }

    /// <summary>
    /// Delete shared layout (Admin only)
    /// </summary>
    [HttpDelete("shared-layouts/{id}")]
    public async Task<ActionResult> DeleteSharedLayout(Guid id)
    {
        try
        {
            var repository = _unitOfWork.Repository<SharedLayout>();
            var layout = await repository.GetByIdAsync(id);
            if (layout == null)
            {
                return NotFound("Shared layout not found");
            }

            await repository.DeleteAsync(layout);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shared layout {Id}", id);
            return StatusCode(500, "An error occurred while deleting shared layout");
        }
    }

    private bool TryGetAdminUserId(out Guid adminUserId)
    {
        adminUserId = Guid.Empty;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out adminUserId);
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

/// <summary>
/// Request model for creating a user
/// </summary>
public class CreateUserRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
    public UserRole Role { get; set; }
}

/// <summary>
/// Request model for updating a user
/// </summary>
public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public UserRole? Role { get; set; }
}

/// <summary>
/// Request model for resetting password
/// </summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = null!;
}
