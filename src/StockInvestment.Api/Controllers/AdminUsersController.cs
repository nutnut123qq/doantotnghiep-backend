using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.Admin.GetAllUsers;
using StockInvestment.Application.Features.Admin.SetUserActiveStatus;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminUsersController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminUsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetAllUsersQuery { Page = page, PageSize = pageSize });
        return Ok(new { users = result.Users, totalCount = result.TotalCount, page, pageSize, totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize) });
    }

    [HttpPut("{userId:guid}/status")]
    public async Task<ActionResult> SetUserActiveStatus(Guid userId, [FromBody] SetUserStatusRequest request)
    {
        var success = await _mediator.Send(new SetUserActiveStatusCommand { AdminUserId = GetRequiredAdminUserId(), UserId = userId, IsActive = request.IsActive });
        if (!success) return BadRequest("Failed to update user status");
        var action = request.IsActive ? "activated" : "deactivated";
        return Ok(new { message = $"User {action} successfully" });
    }

    private Guid GetRequiredAdminUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdValue, out var userId)) return userId;
        throw new UnauthorizedAccessException("User ID not found in token");
    }
}
