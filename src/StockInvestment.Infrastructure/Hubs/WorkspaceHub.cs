using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace StockInvestment.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for workspace collaboration and chat
/// </summary>
[Authorize]
public class WorkspaceHub : Hub
{
    /// <summary>
    /// Join a workspace group for real-time updates
    /// </summary>
    public async Task JoinWorkspace(string workspaceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, workspaceId);
    }
    
    /// <summary>
    /// Leave a workspace group
    /// </summary>
    public async Task LeaveWorkspace(string workspaceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, workspaceId);
    }
    
    /// <summary>
    /// Send a message to workspace group
    /// </summary>
    public async Task SendMessage(string workspaceId, string message)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown User";
        
        var messageObject = new
        {
            id = Guid.NewGuid().ToString(),
            userId = userId,
            userName = userName,
            content = message,
            timestamp = DateTime.UtcNow
        };
        
        // Broadcast to all members of the workspace group
        await Clients.Group(workspaceId).SendAsync("ReceiveMessage", messageObject);
    }
}
