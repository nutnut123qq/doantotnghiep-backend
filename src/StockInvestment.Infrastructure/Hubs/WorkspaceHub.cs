using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Security.Claims;

namespace StockInvestment.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for workspace collaboration and chat
/// </summary>
[Authorize]
public class WorkspaceHub : Hub
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspaceHub> _logger;

    public WorkspaceHub(
        IWorkspaceService workspaceService,
        ILogger<WorkspaceHub> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <summary>
    /// Join a workspace group for real-time updates
    /// </summary>
    public async Task JoinWorkspace(string workspaceId)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        // Verify user is member
        try
        {
            var workspace = await _workspaceService.GetByIdAsync(Guid.Parse(workspaceId), userId);
            if (workspace == null)
            {
                await Clients.Caller.SendAsync("Error", "Workspace not found or access denied");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, workspaceId);
            _logger.LogInformation("User {UserId} joined workspace {WorkspaceId}", userId, workspaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining workspace {WorkspaceId}", workspaceId);
            await Clients.Caller.SendAsync("Error", "Failed to join workspace");
        }
    }
    
    /// <summary>
    /// Leave a workspace group
    /// </summary>
    public async Task LeaveWorkspace(string workspaceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, workspaceId);
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} left workspace {WorkspaceId}", userId, workspaceId);
    }
    
    /// <summary>
    /// Send a message to workspace group and persist to database
    /// </summary>
    public async Task SendMessage(string workspaceId, string message)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown User";
        
        try
        {
            // Save message to database
            var savedMessage = await _workspaceService.SendMessageAsync(
                Guid.Parse(workspaceId), 
                message, 
                userId);

            var messageObject = new
            {
                id = savedMessage.Id.ToString(),
                userId = savedMessage.UserId.ToString(),
                userName = userName,
                content = savedMessage.Content,
                timestamp = savedMessage.CreatedAt
            };
            
            // Broadcast to all members of the workspace group
            await Clients.Group(workspaceId).SendAsync("ReceiveMessage", messageObject);
        }
        catch (UnauthorizedAccessException)
        {
            await Clients.Caller.SendAsync("Error", "You are not a member of this workspace");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to workspace {WorkspaceId}", workspaceId);
            await Clients.Caller.SendAsync("Error", "Failed to send message");
        }
    }

    /// <summary>
    /// Notify workspace members about watchlist update
    /// </summary>
    public async Task NotifyWatchlistUpdate(string workspaceId, string watchlistId)
    {
        await Clients.Group(workspaceId).SendAsync("WatchlistUpdated", new { watchlistId });
    }

    /// <summary>
    /// Notify workspace members about layout update
    /// </summary>
    public async Task NotifyLayoutUpdate(string workspaceId, string layoutId)
    {
        await Clients.Group(workspaceId).SendAsync("LayoutUpdated", new { layoutId });
    }

    /// <summary>
    /// Notify workspace members about new member joined
    /// </summary>
    public async Task NotifyMemberJoined(string workspaceId, object member)
    {
        await Clients.Group(workspaceId).SendAsync("MemberJoined", member);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return Guid.Empty;
    }
}
