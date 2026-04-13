using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.Admin.CreateUser;
using StockInvestment.Application.Features.Admin.GetAllUsers;
using StockInvestment.Application.Features.Admin.LockUser;
using StockInvestment.Application.Features.Admin.ResetUserPassword;
using StockInvestment.Application.Features.Admin.SetUserActiveStatus;
using StockInvestment.Application.Features.Admin.UnlockUser;
using StockInvestment.Application.Features.Admin.UpdateUser;
using StockInvestment.Application.Features.Admin.UpdateUserRole;
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

    [HttpPost]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var command = new CreateUserCommand { AdminUserId = GetRequiredAdminUserId(), Email = request.Email, Password = request.Password, Role = request.Role, FullName = request.FullName };
        var result = await _mediator.Send(command);
        if (!result.Success || result.Data == null) return BadRequest(result.ErrorMessage ?? "Failed to create user");
        return Ok(new { message = "User created successfully", user = result.Data });
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand { AdminUserId = GetRequiredAdminUserId(), UserId = userId, Email = request.Email, FullName = request.FullName, Role = request.Role };
        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result.ErrorMessage ?? "Failed to update user");
        return Ok(new { message = "User updated successfully" });
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<ActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordRequest request)
    {
        var result = await _mediator.Send(new ResetUserPasswordCommand { AdminUserId = GetRequiredAdminUserId(), UserId = userId, NewPassword = request.NewPassword });
        if (!result.Success) return BadRequest(result.ErrorMessage ?? "Failed to reset password");
        return Ok(new { message = "Password reset successfully" });
    }

    [HttpPost("{userId:guid}/lock")]
    public async Task<ActionResult> LockUser(Guid userId)
    {
        var result = await _mediator.Send(new LockUserCommand { AdminUserId = GetRequiredAdminUserId(), UserId = userId });
        if (!result.Success) return BadRequest(result.ErrorMessage ?? "Failed to lock user");
        return Ok(new { message = "User locked successfully" });
    }

    [HttpPost("{userId:guid}/unlock")]
    public async Task<ActionResult> UnlockUser(Guid userId)
    {
        var result = await _mediator.Send(new UnlockUserCommand { AdminUserId = GetRequiredAdminUserId(), UserId = userId });
        if (!result.Success) return BadRequest(result.ErrorMessage ?? "Failed to unlock user");
        return Ok(new { message = "User unlocked successfully" });
    }

    [HttpPut("{userId:guid}/role")]
    public async Task<ActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request)
    {
        var success = await _mediator.Send(new UpdateUserRoleCommand { UserId = userId, NewRole = request.NewRole });
        if (!success) return NotFound($"User with ID {userId} not found");
        return Ok(new { message = "User role updated successfully" });
    }

    [HttpPut("{userId:guid}/status")]
    public async Task<ActionResult> SetUserActiveStatus(Guid userId, [FromBody] SetUserStatusRequest request)
    {
        var success = await _mediator.Send(new SetUserActiveStatusCommand { UserId = userId, IsActive = request.IsActive });
        if (!success) return NotFound($"User with ID {userId} not found");
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
