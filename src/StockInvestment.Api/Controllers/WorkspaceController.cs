using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceController(
        IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    /// <summary>
    /// Get all workspaces for current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWorkspaces()
    {
        var userId = GetRequiredUserId();
        var workspaces = await _workspaceService.GetUserWorkspacesAsync(userId);
        return Ok(workspaces);
    }

    /// <summary>
    /// Get workspace by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkspace(Guid id)
    {
        var userId = GetRequiredUserId();
        var workspace = await _workspaceService.GetByIdAsync(id, userId);
        if (workspace == null)
        {
            return NotFound($"Workspace with ID {id} not found");
        }
        return Ok(workspace);
    }

    /// <summary>
    /// Create a new workspace
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        var userId = GetRequiredUserId();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Workspace name is required");
        }

        var workspace = await _workspaceService.CreateAsync(request.Name, request.Description, userId);
        return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, workspace);
    }

    /// <summary>
    /// Update workspace
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest request)
    {
        var userId = GetRequiredUserId();
        try
        {
            var workspace = await _workspaceService.UpdateAsync(id, request.Name, request.Description, userId);
            return Ok(workspace);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete workspace
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkspace(Guid id)
    {
        var userId = GetRequiredUserId();
        try
        {
            await _workspaceService.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Add member to workspace
    /// </summary>
    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        var userId = GetRequiredUserId();
        try
        {
            var member = await _workspaceService.AddMemberAsync(id, request.UserId, userId, request.Role ?? WorkspaceRole.Member);
            return Ok(member);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Remove member from workspace
    /// </summary>
    [HttpDelete("{id}/members/{memberUserId}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberUserId)
    {
        var userId = GetRequiredUserId();
        try
        {
            await _workspaceService.RemoveMemberAsync(id, memberUserId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Update member role
    /// </summary>
    [HttpPut("{id}/members/{memberUserId}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid memberUserId, [FromBody] UpdateMemberRoleRequest request)
    {
        var userId = GetRequiredUserId();
        try
        {
            await _workspaceService.UpdateMemberRoleAsync(id, memberUserId, request.Role, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get shared watchlists in workspace
    /// </summary>
    [HttpGet("{id}/watchlists")]
    public async Task<IActionResult> GetSharedWatchlists(Guid id)
    {
        var userId = GetRequiredUserId();
        try
        {
            var watchlists = await _workspaceService.GetSharedWatchlistsAsync(id, userId);
            return Ok(watchlists);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Add watchlist to workspace
    /// </summary>
    [HttpPost("{id}/watchlists")]
    public async Task<IActionResult> AddWatchlist(Guid id, [FromBody] AddWatchlistRequest request)
    {
        var userId = GetRequiredUserId();
        try
        {
            await _workspaceService.AddWatchlistAsync(id, request.WatchlistId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Remove watchlist from workspace
    /// </summary>
    [HttpDelete("{id}/watchlists/{watchlistId}")]
    public async Task<IActionResult> RemoveWatchlist(Guid id, Guid watchlistId)
    {
        var userId = GetRequiredUserId();
        try
        {
            await _workspaceService.RemoveWatchlistAsync(id, watchlistId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private Guid GetRequiredUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("User ID not found in token");
    }
}

// Request DTOs
public class CreateWorkspaceRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class UpdateWorkspaceRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class AddMemberRequest
{
    public Guid UserId { get; set; }
    public WorkspaceRole? Role { get; set; }
}

public class UpdateMemberRoleRequest
{
    public WorkspaceRole Role { get; set; }
}

public class AddWatchlistRequest
{
    public Guid WatchlistId { get; set; }
}

