using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.UserPreferences.DeleteUserPreference;
using StockInvestment.Application.Features.UserPreferences.GetUserPreferences;
using StockInvestment.Application.Features.UserPreferences.SaveUserPreference;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserPreferenceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserPreferenceController> _logger;

    public UserPreferenceController(IMediator mediator, ILogger<UserPreferenceController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Get all preferences for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPreferences()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var query = new GetUserPreferencesQuery(userId);
            var preferences = await _mediator.Send(query);

            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user preferences");
            return StatusCode(500, "Error retrieving preferences");
        }
    }

    /// <summary>
    /// Get a specific preference by key
    /// </summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetPreference(string key)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var query = new GetUserPreferencesQuery(userId);
            var preferences = await _mediator.Send(query);
            var preference = preferences.FirstOrDefault(p => p.PreferenceKey == key);

            if (preference == null)
            {
                return NotFound($"Preference with key '{key}' not found");
            }

            return Ok(preference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user preference {Key}", key);
            return StatusCode(500, "Error retrieving preference");
        }
    }

    /// <summary>
    /// Save or update a preference
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SavePreference([FromBody] SavePreferenceRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var command = new SaveUserPreferenceCommand
            {
                UserId = userId,
                PreferenceKey = request.PreferenceKey,
                PreferenceValue = request.PreferenceValue
            };

            var result = await _mediator.Send(command);

            if (result)
            {
                return Ok(new { message = "Preference saved successfully" });
            }

            return BadRequest("Failed to save preference");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user preference");
            return StatusCode(500, "Error saving preference");
        }
    }

    /// <summary>
    /// Delete a preference by key
    /// </summary>
    [HttpDelete("{key}")]
    public async Task<IActionResult> DeletePreference(string key)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var command = new DeleteUserPreferenceCommand
            {
                UserId = userId,
                PreferenceKey = key
            };

            var result = await _mediator.Send(command);

            if (result)
            {
                return Ok(new { message = "Preference deleted successfully" });
            }

            return NotFound($"Preference with key '{key}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user preference {Key}", key);
            return StatusCode(500, "Error deleting preference");
        }
    }
}

public class SavePreferenceRequest
{
    public string PreferenceKey { get; set; } = string.Empty;
    public string PreferenceValue { get; set; } = string.Empty;
}
