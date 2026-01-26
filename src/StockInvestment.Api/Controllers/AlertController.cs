using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.Alerts.CreateAlert;
using StockInvestment.Application.Features.Alerts.GetAlerts;
using StockInvestment.Application.Features.Alerts.UpdateAlert;
using StockInvestment.Application.Features.Alerts.DeleteAlert;
using StockInvestment.Application.Features.Alerts.ToggleAlert;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using StockInvestment.Domain.Exceptions;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAIService _aiService;
    private readonly ILogger<AlertController> _logger;

    public AlertController(IMediator mediator, IAIService aiService, ILogger<AlertController> logger)
    {
        _mediator = mediator;
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Get all alerts for current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAlerts([FromQuery] bool? isActive = null)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new GetAlertsQuery
        {
            UserId = userId,
            IsActive = isActive
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Parse natural language alert input without creating the alert
    /// </summary>
    [HttpPost("parse")]
    public async Task<IActionResult> ParseAlert([FromBody] ParseAlertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NaturalLanguageInput))
        {
            return BadRequest("NaturalLanguageInput is required");
        }

        try
        {
            var parsedAlert = await _aiService.ParseAlertAsync(request.NaturalLanguageInput);
            return Ok(parsedAlert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing alert");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Create a new alert (with NLP support)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAlert([FromBody] CreateAlertRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new CreateAlertCommand
        {
            UserId = userId,
            Symbol = request.Symbol,
            NaturalLanguageInput = request.NaturalLanguageInput,
            Type = request.Type,
            Condition = request.Condition,
            Threshold = request.Threshold,
            Timeframe = request.Timeframe
        };

        try
        {
            var result = await _mediator.Send(command);
            return Created("", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update an alert
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAlert(Guid id, [FromBody] UpdateAlertRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new UpdateAlertCommand
        {
            AlertId = id,
            UserId = userId,
            Symbol = request.Symbol,
            Type = request.Type,
            Condition = request.Condition,
            Threshold = request.Threshold,
            Timeframe = request.Timeframe
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert {AlertId}", id);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Delete an alert
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAlert(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new DeleteAlertCommand
        {
            AlertId = id,
            UserId = userId
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert {AlertId}", id);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Toggle alert active status
    /// </summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> ToggleAlert(Guid id, [FromBody] ToggleAlertRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new ToggleAlertCommand
        {
            AlertId = id,
            UserId = userId,
            IsActive = request.IsActive
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling alert {AlertId}", id);
            return BadRequest(ex.Message);
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

public class ParseAlertRequest
{
    public string NaturalLanguageInput { get; set; } = string.Empty;
}

public class CreateAlertRequest
{
    public string? Symbol { get; set; }
    public string? NaturalLanguageInput { get; set; }
    public AlertType? Type { get; set; }
    public string? Condition { get; set; }
    public decimal? Threshold { get; set; }
    public string? Timeframe { get; set; }
}

public class UpdateAlertRequest
{
    public string? Symbol { get; set; }
    public AlertType? Type { get; set; }
    public string? Condition { get; set; }
    public decimal? Threshold { get; set; }
    public string? Timeframe { get; set; }
}

public class ToggleAlertRequest
{
    public bool IsActive { get; set; }
}

