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
    private readonly ILogger<WorkspaceController> _logger;

    public WorkspaceController(
        IWorkspaceService workspaceService,
        ILogger<WorkspaceController> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <summary>
    /// Get all workspaces for current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWorkspaces()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var workspaces = await _workspaceService.GetUserWorkspacesAsync(userId);
            return Ok(workspaces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspaces");
            return StatusCode(500, "An error occurred while retrieving workspaces");
        }
    }

    /// <summary>
    /// Get workspace by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkspace(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var workspace = await _workspaceService.GetByIdAsync(id, userId);
            if (workspace == null)
            {
                return NotFound($"Workspace with ID {id} not found");
            }

            return Ok(workspace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspace {Id}", id);
            return StatusCode(500, "An error occurred while retrieving workspace");
        }
    }

    /// <summary>
    /// Create a new workspace
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Workspace name is required");
            }

            var workspace = await _workspaceService.CreateAsync(request.Name, request.Description, userId);
            return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, workspace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workspace");
            return StatusCode(500, "An error occurred while creating workspace");
        }
    }

    /// <summary>
    /// Update workspace
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var workspace = await _workspaceService.UpdateAsync(id, request.Name, request.Description, userId);
            return Ok(workspace);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workspace {Id}", id);
            return StatusCode(500, "An error occurred while updating workspace");
        }
    }

    /// <summary>
    /// Delete workspace
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkspace(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _workspaceService.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workspace {Id}", id);
            return StatusCode(500, "An error occurred while deleting workspace");
        }
    }

    /// <summary>
    /// Add member to workspace
    /// </summary>
    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var member = await _workspaceService.AddMemberAsync(id, request.UserId, userId, request.Role ?? WorkspaceRole.Member);
            return Ok(member);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to workspace {Id}", id);
            return StatusCode(500, "An error occurred while adding member");
        }
    }

    /// <summary>
    /// Remove member from workspace
    /// </summary>
    [HttpDelete("{id}/members/{memberUserId}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberUserId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _workspaceService.RemoveMemberAsync(id, memberUserId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from workspace {Id}", id);
            return StatusCode(500, "An error occurred while removing member");
        }
    }

    /// <summary>
    /// Update member role
    /// </summary>
    [HttpPut("{id}/members/{memberUserId}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid memberUserId, [FromBody] UpdateMemberRoleRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _workspaceService.UpdateMemberRoleAsync(id, memberUserId, request.Role, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member role in workspace {Id}", id);
            return StatusCode(500, "An error occurred while updating member role");
        }
    }

    /// <summary>
    /// Get shared watchlists in workspace
    /// </summary>
    [HttpGet("{id}/watchlists")]
    public async Task<IActionResult> GetSharedWatchlists(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var watchlists = await _workspaceService.GetSharedWatchlistsAsync(id, userId);
            return Ok(watchlists);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared watchlists for workspace {Id}", id);
            return StatusCode(500, "An error occurred while retrieving watchlists");
        }
    }

    /// <summary>
    /// Add watchlist to workspace
    /// </summary>
    [HttpPost("{id}/watchlists")]
    public async Task<IActionResult> AddWatchlist(Guid id, [FromBody] AddWatchlistRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _workspaceService.AddWatchlistAsync(id, request.WatchlistId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding watchlist to workspace {Id}", id);
            return StatusCode(500, "An error occurred while adding watchlist");
        }
    }

    /// <summary>
    /// Remove watchlist from workspace
    /// </summary>
    [HttpDelete("{id}/watchlists/{watchlistId}")]
    public async Task<IActionResult> RemoveWatchlist(Guid id, Guid watchlistId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _workspaceService.RemoveWatchlistAsync(id, watchlistId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing watchlist from workspace {Id}", id);
            return StatusCode(500, "An error occurred while removing watchlist");
        }
    }

    /// <summary>
    /// Get shared layouts in workspace
    /// </summary>
    [HttpGet("{id}/layouts")]
    public async Task<IActionResult> GetSharedLayouts(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var layouts = await _workspaceService.GetSharedLayoutsAsync(id, userId);
            return Ok(layouts);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared layouts for workspace {Id}", id);
            return StatusCode(500, "An error occurred while retrieving layouts");
        }
    }

    /// <summary>
    /// Add layout to workspace
    /// </summary>
    [HttpPost("{id}/layouts")]
    public async Task<IActionResult> AddLayout(Guid id, [FromBody] AddLayoutRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _workspaceService.AddLayoutAsync(id, request.LayoutId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding layout to workspace {Id}", id);
            return StatusCode(500, "An error occurred while adding layout");
        }
    }

    /// <summary>
    /// Remove layout from workspace
    /// </summary>
    [HttpDelete("{id}/layouts/{layoutId}")]
    public async Task<IActionResult> RemoveLayout(Guid id, Guid layoutId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            await _workspaceService.RemoveLayoutAsync(id, layoutId, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing layout from workspace {Id}", id);
            return StatusCode(500, "An error occurred while removing layout");
        }
    }

    /// <summary>
    /// Get messages in workspace
    /// </summary>
    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var messages = await _workspaceService.GetMessagesAsync(id, userId, limit);
            return Ok(messages);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for workspace {Id}", id);
            return StatusCode(500, "An error occurred while retrieving messages");
        }
    }

    /// <summary>
    /// Send message to workspace
    /// </summary>
    [HttpPost("{id}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest("Message content is required");
            }

            var message = await _workspaceService.SendMessageAsync(id, request.Content, userId);
            return Ok(message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to workspace {Id}", id);
            return StatusCode(500, "An error occurred while sending message");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return Guid.Empty;
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

public class AddLayoutRequest
{
    public Guid LayoutId { get; set; }
}

public class SendMessageRequest
{
    public string Content { get; set; } = null!;
}
