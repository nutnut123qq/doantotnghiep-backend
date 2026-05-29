using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Contracts.Responses;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/admin/news")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminNewsController : ControllerBase
{
    private readonly INewsService _newsService;

    public AdminNewsController(INewsService newsService)
    {
        _newsService = newsService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? tickerId = null)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _newsService.GetNewsForAdminAsync(safePage, safePageSize, tickerId);
        return Ok(new PagedResponse<object>
        {
            Items = result.Items.Cast<object>().ToList(),
            TotalCount = result.TotalCount,
            PageNumber = safePage,
            PageSize = safePageSize
        });
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetNewsDeleted(Guid id, [FromBody] SetNewsDeletedRequest? body)
    {
        if (body == null)
        {
            return BadRequest();
        }

        var updated = await _newsService.SetNewsDeletedAsync(id, body.IsDeleted);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Backfill TickerId on news rows that lack symbol tagging.
    /// </summary>
    [HttpPost("backfill-ticker-ids")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> BackfillTickerIds(
        [FromQuery] int batchSize = 500,
        CancellationToken cancellationToken = default)
    {
        var updated = await _newsService.BackfillTickerIdsAsync(batchSize, cancellationToken);
        return Ok(new { updated });
    }
}

public class SetNewsDeletedRequest
{
    public bool IsDeleted { get; set; }
}
