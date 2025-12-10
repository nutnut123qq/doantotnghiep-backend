using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Attributes;
using StockInvestment.Api.Middleware;
using StockInvestment.Application.Features.Users.GetUsers;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMediator mediator, ILogger<UsersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of users (cached for 5 minutes)
    /// </summary>
    [HttpGet]
    [Cached(300)] // Cache for 5 minutes (300 seconds)
    [ProducesResponseType(typeof(GetUsersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null)
    {
        var query = new GetUsersQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Role = role,
            IsActive = isActive
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }
}

