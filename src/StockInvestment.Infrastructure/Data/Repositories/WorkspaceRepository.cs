using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Workspace
/// </summary>
public class WorkspaceRepository : Repository<Workspace>, IWorkspaceRepository
{
    public WorkspaceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Workspace>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(w => w.Members)
            .Include(w => w.Owner)
            .Where(w => w.OwnerId == userId || w.Members.Any(m => m.UserId == userId))
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Workspace?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(w => w.Owner)
            .Include(w => w.Members)
                .ThenInclude(m => m.User)
            .Include(w => w.Watchlists)
                .ThenInclude(ww => ww.Watchlist)
            .Include(w => w.Layouts)
                .ThenInclude(wl => wl.Layout)
            .Include(w => w.Messages.OrderByDescending(m => m.CreatedAt).Take(50))
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
    {
        var workspace = await _dbSet
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);

        if (workspace == null) return false;

        return workspace.OwnerId == userId || workspace.Members.Any(m => m.UserId == userId);
    }

    public async Task<bool> HasPermissionAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
    {
        var workspace = await _dbSet
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);

        if (workspace == null) return false;

        if (workspace.OwnerId == userId) return true;

        var member = workspace.Members.FirstOrDefault(m => m.UserId == userId);
        return member != null && (member.Role == WorkspaceRole.Owner || member.Role == WorkspaceRole.Admin);
    }
}
