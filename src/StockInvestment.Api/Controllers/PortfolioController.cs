using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;
    private readonly ILogger<PortfolioController> _logger;

    public PortfolioController(
        IPortfolioService portfolioService,
        ILogger<PortfolioController> logger)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    /// <summary>
    /// Get all holdings for current user
    /// </summary>
    [HttpGet("holdings")]
    public async Task<IActionResult> GetHoldings()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var holdings = await _portfolioService.GetHoldingsAsync(userId);
            return Ok(holdings);
    }

    /// <summary>
    /// Get portfolio summary for current user
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized("User ID not found in token");
        }

        var summary = await _portfolioService.GetSummaryAsync(userId);
        return Ok(summary);
    }

    /// <summary>
    /// Add a new holding to portfolio
    /// </summary>
    [HttpPost("holdings")]
    public async Task<IActionResult> AddHolding([FromBody] AddHoldingRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            if (request.Shares <= 0 || request.AvgPrice <= 0)
            {
                return BadRequest("Shares and average price must be greater than 0");
            }

            var holding = await _portfolioService.AddHoldingAsync(userId, request);
            return CreatedAtAction(nameof(GetHoldings), new { id = holding.Id }, holding);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request to add holding");
            return BadRequest(ex.Message);
        }
        // Let GlobalExceptionHandlerMiddleware handle other exceptions
    }

    /// <summary>
    /// Update an existing holding
    /// </summary>
    [HttpPut("holdings/{id}")]
    public async Task<IActionResult> UpdateHolding(Guid id, [FromBody] UpdateHoldingRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            if (request.Shares <= 0 || request.AvgPrice <= 0)
            {
                return BadRequest("Shares and average price must be greater than 0");
            }

            var holding = await _portfolioService.UpdateHoldingAsync(userId, id, request);
            return Ok(holding);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Holding not found");
            return NotFound(ex.Message);
        }
        // Let GlobalExceptionHandlerMiddleware handle other exceptions
    }

    /// <summary>
    /// Delete a holding from portfolio
    /// </summary>
    [HttpDelete("holdings/{id}")]
    public async Task<IActionResult> DeleteHolding(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _portfolioService.DeleteHoldingAsync(userId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Holding not found");
            return NotFound(ex.Message);
        }
        // Let GlobalExceptionHandlerMiddleware handle other exceptions
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
