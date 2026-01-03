using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.CorporateEvents.CreateEvent;
using StockInvestment.Application.Features.CorporateEvents.GetCorporateEvents;
using StockInvestment.Application.Features.CorporateEvents.GetEventById;
using StockInvestment.Application.Features.CorporateEvents.GetUpcomingEvents;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CorporateEventController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CorporateEventController> _logger;

    public CorporateEventController(IMediator mediator, ILogger<CorporateEventController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all corporate events with optional filtering
    /// </summary>
    /// <param name="symbol">Filter by stock symbol</param>
    /// <param name="eventType">Filter by event type</param>
    /// <param name="startDate">Filter by start date</param>
    /// <param name="endDate">Filter by end date</param>
    /// <param name="status">Filter by event status</param>
    /// <returns>List of corporate events</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CorporateEvent>>> GetEvents(
        [FromQuery] string? symbol = null,
        [FromQuery] CorporateEventType? eventType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] EventStatus? status = null)
    {
        try
        {
            var query = new GetCorporateEventsQuery
            {
                Symbol = symbol,
                EventType = eventType,
                StartDate = startDate,
                EndDate = endDate,
                Status = status
            };

            var events = await _mediator.Send(query);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting corporate events");
            return StatusCode(500, "An error occurred while retrieving events");
        }
    }

    /// <summary>
    /// Get upcoming corporate events
    /// </summary>
    /// <param name="daysAhead">Number of days to look ahead (default: 30)</param>
    /// <returns>List of upcoming events</returns>
    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<CorporateEvent>>> GetUpcomingEvents(
        [FromQuery] int daysAhead = 30)
    {
        try
        {
            var query = new GetUpcomingEventsQuery { DaysAhead = daysAhead };
            var events = await _mediator.Send(query);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upcoming events");
            return StatusCode(500, "An error occurred while retrieving upcoming events");
        }
    }

    /// <summary>
    /// Get corporate event by ID
    /// </summary>
    /// <param name="id">Event ID</param>
    /// <returns>Corporate event details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<CorporateEvent>> GetEventById(Guid id)
    {
        try
        {
            var query = new GetEventByIdQuery(id);
            var corporateEvent = await _mediator.Send(query);

            if (corporateEvent == null)
            {
                return NotFound($"Event with ID {id} not found");
            }

            return Ok(corporateEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event by ID: {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the event");
        }
    }

    /// <summary>
    /// Create a new corporate event
    /// </summary>
    /// <param name="command">Event creation data</param>
    /// <returns>Created event</returns>
    [HttpPost]
    public async Task<ActionResult<CorporateEvent>> CreateEvent([FromBody] CreateEventCommand command)
    {
        try
        {
            var corporateEvent = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetEventById), new { id = corporateEvent.Id }, corporateEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating corporate event");
            return StatusCode(500, "An error occurred while creating the event");
        }
    }
}
