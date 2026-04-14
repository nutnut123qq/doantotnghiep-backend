using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Contracts.Responses;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/admin/corporate-events")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminCorporateEventsController : ControllerBase
{
    private readonly ICorporateEventService _corporateEventService;

    public AdminCorporateEventsController(ICorporateEventService corporateEventService)
    {
        _corporateEventService = corporateEventService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? symbol = null,
        [FromQuery] CorporateEventType? eventType = null,
        [FromQuery] EventStatus? status = null)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _corporateEventService.GetEventsForAdminAsync(safePage, safePageSize, symbol, eventType, status);
        var items = result.Items.Select(e => new
        {
            e.Id,
            e.StockTickerId,
            symbol = e.StockTicker?.Symbol,
            e.EventType,
            e.EventDate,
            e.Title,
            e.Description,
            e.SourceUrl,
            e.Status,
            e.CreatedAt,
            e.UpdatedAt,
            e.IsDeleted
        }).Cast<object>().ToList();
        return Ok(new PagedResponse<object>
        {
            Items = items,
            TotalCount = result.TotalCount,
            PageNumber = safePage,
            PageSize = safePageSize
        });
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEventDeleted(Guid id, [FromBody] SetCorporateEventDeletedRequest? body)
    {
        if (body == null)
        {
            return BadRequest();
        }

        var updated = await _corporateEventService.SetEventDeletedAsync(id, body.IsDeleted);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }
}

public class SetCorporateEventDeletedRequest
{
    public bool IsDeleted { get; set; }
}
