using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Workspace repository interface
/// </summary>
public interface IWorkspaceRepository : IRepository<Workspace>
{
    /// <summary>
    /// Get workspaces for a user (as owner or member)
    /// </summary>
    Task<IEnumerable<Workspace>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get workspace with members, watchlists, and layouts
    /// </summary>
    Task<Workspace?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user is member of workspace
    /// </summary>
    Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user has permission (Owner or Admin)
    /// </summary>
    Task<bool> HasPermissionAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
}
