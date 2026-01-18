using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Workspace service implementation
/// </summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(
        IUnitOfWork unitOfWork,
        ILogger<WorkspaceService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<Workspace>> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Workspaces.GetByUserIdAsync(userId, cancellationToken);
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var workspace = await _unitOfWork.Workspaces.GetByIdWithDetailsAsync(id, cancellationToken);
        
        if (workspace == null) return null;
        
        // Check if user has access
        if (!await _unitOfWork.Workspaces.IsMemberAsync(id, userId, cancellationToken))
        {
            return null;
        }

        return workspace;
    }

    public async Task<Workspace> CreateAsync(string name, string? description, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var workspace = new Workspace
        {
            Name = name,
            Description = description,
            OwnerId = ownerId,
        };

        await _unitOfWork.Workspaces.AddAsync(workspace, cancellationToken);
        
        // Add owner as member with Owner role
        var ownerMember = new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = ownerId,
            Role = WorkspaceRole.Owner,
        };
        await _unitOfWork.Repository<WorkspaceMember>().AddAsync(ownerMember, cancellationToken);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Created workspace {WorkspaceId} by user {UserId}", workspace.Id, ownerId);
        
        return workspace;
    }

    public async Task<Workspace> UpdateAsync(Guid id, string name, string? description, Guid userId, CancellationToken cancellationToken = default)
    {
        var workspace = await _unitOfWork.Workspaces.GetByIdAsync(id, cancellationToken);
        if (workspace == null)
        {
            throw new ArgumentException("Workspace not found");
        }

        if (!await _unitOfWork.Workspaces.HasPermissionAsync(id, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to update workspace");
        }

        workspace.Name = name;
        workspace.Description = description;
        workspace.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Workspaces.UpdateAsync(workspace);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return workspace;
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var workspace = await _unitOfWork.Workspaces.GetByIdAsync(id, cancellationToken);
        if (workspace == null)
        {
            throw new ArgumentException("Workspace not found");
        }

        if (workspace.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("Only workspace owner can delete workspace");
        }

        await _unitOfWork.Workspaces.DeleteAsync(workspace);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkspaceMember> AddMemberAsync(Guid workspaceId, Guid userId, Guid addedByUserId, WorkspaceRole role = WorkspaceRole.Member, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.HasPermissionAsync(workspaceId, addedByUserId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to add members");
        }

        if (await _unitOfWork.Workspaces.IsMemberAsync(workspaceId, userId, cancellationToken))
        {
            throw new InvalidOperationException("User is already a member of this workspace");
        }

        var member = new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Role = role,
        };

        await _unitOfWork.Repository<WorkspaceMember>().AddAsync(member, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return member;
    }

    public async Task RemoveMemberAsync(Guid workspaceId, Guid userId, Guid removedByUserId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.HasPermissionAsync(workspaceId, removedByUserId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to remove members");
        }

        var member = await _unitOfWork.Repository<WorkspaceMember>()
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken);

        if (member == null)
        {
            throw new ArgumentException("Member not found");
        }

        await _unitOfWork.Repository<WorkspaceMember>().DeleteAsync(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMemberRoleAsync(Guid workspaceId, Guid userId, WorkspaceRole newRole, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.HasPermissionAsync(workspaceId, updatedByUserId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to update member roles");
        }

        var member = await _unitOfWork.Repository<WorkspaceMember>()
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken);

        if (member == null)
        {
            throw new ArgumentException("Member not found");
        }

        member.Role = newRole;
        await _unitOfWork.Repository<WorkspaceMember>().UpdateAsync(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<Watchlist>> GetSharedWatchlistsAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.IsMemberAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User is not a member of this workspace");
        }

        var workspace = await _unitOfWork.Workspaces.GetByIdWithDetailsAsync(workspaceId, cancellationToken);
        return workspace?.Watchlists.Select(ww => ww.Watchlist) ?? Enumerable.Empty<Watchlist>();
    }

    public async Task AddWatchlistAsync(Guid workspaceId, Guid watchlistId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.HasPermissionAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to add watchlists");
        }

        var workspaceWatchlist = new WorkspaceWatchlist
        {
            WorkspaceId = workspaceId,
            WatchlistId = watchlistId,
            AddedByUserId = userId,
        };

        await _unitOfWork.Repository<WorkspaceWatchlist>().AddAsync(workspaceWatchlist, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveWatchlistAsync(Guid workspaceId, Guid watchlistId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.HasPermissionAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to remove watchlists");
        }

        var workspaceWatchlist = await _unitOfWork.Repository<WorkspaceWatchlist>()
            .FirstOrDefaultAsync(ww => ww.WorkspaceId == workspaceId && ww.WatchlistId == watchlistId, cancellationToken);

        if (workspaceWatchlist != null)
        {
            await _unitOfWork.Repository<WorkspaceWatchlist>().DeleteAsync(workspaceWatchlist);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<Layout>> GetSharedLayoutsAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.IsMemberAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User is not a member of this workspace");
        }

        var workspace = await _unitOfWork.Workspaces.GetByIdWithDetailsAsync(workspaceId, cancellationToken);
        return workspace?.Layouts.Select(wl => wl.Layout) ?? Enumerable.Empty<Layout>();
    }

    public async Task AddLayoutAsync(Guid workspaceId, Guid layoutId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.HasPermissionAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to add layouts");
        }

        var workspaceLayout = new WorkspaceLayout
        {
            WorkspaceId = workspaceId,
            LayoutId = layoutId,
            AddedByUserId = userId,
        };

        await _unitOfWork.Repository<WorkspaceLayout>().AddAsync(workspaceLayout, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveLayoutAsync(Guid workspaceId, Guid layoutId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.HasPermissionAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have permission to remove layouts");
        }

        var workspaceLayout = await _unitOfWork.Repository<WorkspaceLayout>()
            .FirstOrDefaultAsync(wl => wl.WorkspaceId == workspaceId && wl.LayoutId == layoutId, cancellationToken);

        if (workspaceLayout != null)
        {
            await _unitOfWork.Repository<WorkspaceLayout>().DeleteAsync(workspaceLayout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<WorkspaceMessage>> GetMessagesAsync(Guid workspaceId, Guid userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.IsMemberAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User is not a member of this workspace");
        }

        var messages = await _unitOfWork.Repository<WorkspaceMessage>()
            .FindAsync(m => m.WorkspaceId == workspaceId, cancellationToken);

        return messages
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt);
    }

    public async Task<WorkspaceMessage> SendMessageAsync(Guid workspaceId, string content, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Workspaces.IsMemberAsync(workspaceId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User is not a member of this workspace");
        }

        var message = new WorkspaceMessage
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Content = content,
        };

        await _unitOfWork.Repository<WorkspaceMessage>().AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return message;
    }
}
