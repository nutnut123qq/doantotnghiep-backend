using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var news = await _newsService.GetNewsForAdminAsync(page, pageSize, tickerId);
        return Ok(news ?? Enumerable.Empty<object>());
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
}

public class SetNewsDeletedRequest
{
    public bool IsDeleted { get; set; }
}
