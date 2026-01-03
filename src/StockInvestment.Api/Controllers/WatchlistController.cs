using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.Watchlist.GetWatchlists;
using StockInvestment.Application.Features.Watchlist.CreateWatchlist;
using StockInvestment.Application.Features.Watchlist.AddStockToWatchlist;
using StockInvestment.Application.Features.Watchlist.RemoveStockFromWatchlist;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WatchlistController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WatchlistController> _logger;

    public WatchlistController(IMediator mediator, ILogger<WatchlistController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all watchlists for current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWatchlists()
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new GetWatchlistsQuery { UserId = userId };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Create a new watchlist
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateWatchlist([FromBody] CreateWatchlistRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var command = new CreateWatchlistCommand
        {
            UserId = userId,
            Name = request.Name
        };

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetWatchlists), new { id = result.Id }, result);
    }

    /// <summary>
    /// Add stock to watchlist
    /// </summary>
    [HttpPost("{watchlistId}/stocks")]
    public async Task<IActionResult> AddStock(Guid watchlistId, [FromBody] AddStockRequest request)
    {
        var command = new AddStockToWatchlistCommand
        {
            WatchlistId = watchlistId,
            Symbol = request.Symbol
        };

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(result.Message);
        }

        return Ok(result);
    }

    /// <summary>
    /// Remove stock from watchlist
    /// </summary>
    [HttpDelete("{watchlistId}/stocks/{symbol}")]
    public async Task<IActionResult> RemoveStock(Guid watchlistId, string symbol)
    {
        var command = new RemoveStockFromWatchlistCommand
        {
            WatchlistId = watchlistId,
            Symbol = symbol
        };

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(result.Message);
        }

        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

public class CreateWatchlistRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AddStockRequest
{
    public string Symbol { get; set; } = string.Empty;
}

