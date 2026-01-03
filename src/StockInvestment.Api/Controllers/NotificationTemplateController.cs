using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Attributes;
using StockInvestment.Application.Features.Admin.NotificationTemplates.GetNotificationTemplates;
using StockInvestment.Application.Features.Admin.NotificationTemplates.CreateNotificationTemplate;
using StockInvestment.Application.Features.Admin.NotificationTemplates.UpdateNotificationTemplate;
using StockInvestment.Application.Features.Admin.NotificationTemplates.DeleteNotificationTemplate;
using StockInvestment.Application.Features.Admin.NotificationTemplates.PreviewTemplate;
using StockInvestment.Application.Features.Admin.PushNotification.GetConfig;
using StockInvestment.Application.Features.Admin.PushNotification.UpdateConfig;
using StockInvestment.Application.Features.Admin.PushNotification.Test;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[AdminOnly]
public class NotificationTemplateController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<NotificationTemplateController> _logger;

    public NotificationTemplateController(IMediator mediator, ILogger<NotificationTemplateController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all notification templates (Admin only)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<GetNotificationTemplatesResponse>> GetAll([FromQuery] NotificationEventType? eventType)
    {
        try
        {
            var query = new GetNotificationTemplatesQuery { EventType = eventType };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification templates");
            return StatusCode(500, "An error occurred while fetching templates");
        }
    }

    /// <summary>
    /// Create a new notification template (Admin only)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.NotificationTemplates.CreateNotificationTemplate.NotificationTemplateDto>> Create([FromBody] CreateNotificationTemplateCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification template");
            return StatusCode(500, "An error occurred while creating template");
        }
    }

    /// <summary>
    /// Update a notification template (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.NotificationTemplates.UpdateNotificationTemplate.NotificationTemplateDto>> Update(Guid id, [FromBody] UpdateNotificationTemplateCommand command)
    {
        try
        {
            command.Id = id;
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification template {Id}", id);
            return StatusCode(500, "An error occurred while updating template");
        }
    }

    /// <summary>
    /// Delete a notification template (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var command = new DeleteNotificationTemplateCommand { Id = id };
            await _mediator.Send(command);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification template {Id}", id);
            return StatusCode(500, "An error occurred while deleting template");
        }
    }

    /// <summary>
    /// Preview a notification template with sample data (Admin only)
    /// </summary>
    [HttpPost("{id}/preview")]
    public async Task<ActionResult<PreviewTemplateResponse>> Preview(Guid id, [FromBody] Dictionary<string, string> sampleData)
    {
        try
        {
            var command = new PreviewTemplateCommand
            {
                TemplateId = id,
                SampleData = sampleData,
            };
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing template {Id}", id);
            return StatusCode(500, "An error occurred while previewing template");
        }
    }

    /// <summary>
    /// Get push notification configuration (Admin only)
    /// </summary>
    [HttpGet("push-notification/config")]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.PushNotification.GetConfig.PushNotificationConfigDto>> GetPushConfig()
    {
        try
        {
            var query = new GetPushNotificationConfigQuery();
            var result = await _mediator.Send(query);
            
            if (result == null)
            {
                return NotFound("Push notification configuration not found");
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting push notification config");
            return StatusCode(500, "An error occurred while fetching config");
        }
    }

    /// <summary>
    /// Update push notification configuration (Admin only)
    /// </summary>
    [HttpPut("push-notification/config")]
    public async Task<ActionResult<StockInvestment.Application.Features.Admin.PushNotification.UpdateConfig.PushNotificationConfigDto>> UpdatePushConfig([FromBody] UpdatePushNotificationConfigCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating push notification config");
            return StatusCode(500, "An error occurred while updating config");
        }
    }

    /// <summary>
    /// Test push notification (Admin only)
    /// </summary>
    [HttpPost("push-notification/test")]
    public async Task<ActionResult<TestPushNotificationResponse>> TestPushNotification([FromBody] TestPushNotificationCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing push notification");
            return StatusCode(500, "An error occurred while testing notification");
        }
    }
}

