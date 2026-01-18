using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Workspace service interface
/// </summary>
public interface IWorkspaceService
{
    Task<IEnumerable<Workspace>> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Workspace?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Workspace> CreateAsync(string name, string? description, Guid ownerId, CancellationToken cancellationToken = default);
    Task<Workspace> UpdateAsync(Guid id, string name, string? description, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    
    // Members
    Task<WorkspaceMember> AddMemberAsync(Guid workspaceId, Guid userId, Guid addedByUserId, WorkspaceRole role = WorkspaceRole.Member, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid workspaceId, Guid userId, Guid removedByUserId, CancellationToken cancellationToken = default);
    Task UpdateMemberRoleAsync(Guid workspaceId, Guid userId, WorkspaceRole newRole, Guid updatedByUserId, CancellationToken cancellationToken = default);
    
    // Watchlists
    Task<IEnumerable<Watchlist>> GetSharedWatchlistsAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
    Task AddWatchlistAsync(Guid workspaceId, Guid watchlistId, Guid userId, CancellationToken cancellationToken = default);
    Task RemoveWatchlistAsync(Guid workspaceId, Guid watchlistId, Guid userId, CancellationToken cancellationToken = default);
    
    // Layouts
    Task<IEnumerable<Layout>> GetSharedLayoutsAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
    Task AddLayoutAsync(Guid workspaceId, Guid layoutId, Guid userId, CancellationToken cancellationToken = default);
    Task RemoveLayoutAsync(Guid workspaceId, Guid layoutId, Guid userId, CancellationToken cancellationToken = default);
    
    // Messages
    Task<IEnumerable<WorkspaceMessage>> GetMessagesAsync(Guid workspaceId, Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    Task<WorkspaceMessage> SendMessageAsync(Guid workspaceId, string content, Guid userId, CancellationToken cancellationToken = default);
}
