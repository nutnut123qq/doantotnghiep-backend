using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.Alerts.CreateAlert;
using StockInvestment.Application.Features.Alerts.GetAlerts;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
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

