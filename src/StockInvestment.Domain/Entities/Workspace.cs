namespace StockInvestment.Domain.Entities;

public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User Owner { get; set; } = null!;
    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<WorkspaceWatchlist> Watchlists { get; set; } = new List<WorkspaceWatchlist>();
    public ICollection<WorkspaceLayout> Layouts { get; set; } = new List<WorkspaceLayout>();
    public ICollection<WorkspaceMessage> Messages { get; set; } = new List<WorkspaceMessage>();

    public Workspace()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
