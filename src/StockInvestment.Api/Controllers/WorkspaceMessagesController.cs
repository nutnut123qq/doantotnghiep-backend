using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using System.Security.Claims;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/workspace/{workspaceId:guid}/messages")]
[Authorize]
public class WorkspaceMessagesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceMessagesController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(Guid workspaceId, [FromQuery] int limit = 50)
    {
        var userId = GetRequiredUserId();
        var messages = await _workspaceService.GetMessagesAsync(workspaceId, userId, limit);
        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(Guid workspaceId, [FromBody] SendWorkspaceMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Message content is required");
        }

        var userId = GetRequiredUserId();
        var message = await _workspaceService.SendMessageAsync(workspaceId, request.Content, userId);
        return Ok(message);
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

public class SendWorkspaceMessageRequest
{
    public string Content { get; set; } = null!;
}

