using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/notification-channels")]
[Authorize]
public class NotificationChannelController : ControllerBase
{
    private readonly INotificationChannelService _service;
    private readonly ILogger<NotificationChannelController> _logger;

    public NotificationChannelController(
        INotificationChannelService service,
        ILogger<NotificationChannelController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's notification channel configuration
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<NotificationChannelConfigDto>> GetMyConfig(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var config = await _service.GetUserConfigAsync(userId, cancellationToken);

            if (config == null)
                return Ok(new NotificationChannelConfigDto());  // Default empty

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification channel config");
            return StatusCode(500, new { message = "Failed to load configuration" });
        }
    }

    /// <summary>
    /// Update current user's notification channel configuration
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<NotificationChannelConfigDto>> UpdateMyConfig(
        [FromBody] UpdateNotificationChannelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var config = await _service.SaveConfigAsync(userId, request, cancellationToken);
            return Ok(config);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification channel config");
            return StatusCode(500, new { message = "Failed to update configuration" });
        }
    }

    /// <summary>
    /// Test notification channel with saved configuration
    /// </summary>
    [HttpPost("me/test")]
    public async Task<ActionResult> TestChannel(
        [FromBody] TestChannelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();

            // TryParse với ignoreCase để tránh 500 error
            if (!Enum.TryParse<NotificationChannelType>(request.Channel, ignoreCase: true, out var channelType))
            {
                return BadRequest(new { message = "Invalid channel. Must be 'Slack' or 'Telegram'" });
            }

            var success = await _service.TestChannelAsync(userId, channelType, cancellationToken);

            return Ok(new { success, message = success ? "Test notification sent" : "Failed to send test notification" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing notification channel");
            return StatusCode(500, new { message = "Failed to send test notification" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User not authenticated");

        return Guid.Parse(userIdClaim);
    }
}
