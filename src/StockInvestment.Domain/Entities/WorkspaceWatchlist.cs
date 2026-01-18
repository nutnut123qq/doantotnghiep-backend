namespace StockInvestment.Domain.Entities;

/// <summary>
/// Many-to-many relationship between Workspace and Watchlist
/// </summary>
public class WorkspaceWatchlist
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid WatchlistId { get; set; }
    public DateTime AddedAt { get; set; }
    public Guid AddedByUserId { get; set; }

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public Watchlist Watchlist { get; set; } = null!;

    public WorkspaceWatchlist()
    {
        Id = Guid.NewGuid();
        AddedAt = DateTime.UtcNow;
    }
}
