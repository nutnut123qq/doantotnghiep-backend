namespace StockInvestment.Domain.Entities;

/// <summary>
/// Many-to-many relationship between Workspace and Layout
/// Allows sharing layouts within workspace
/// </summary>
public class WorkspaceLayout
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid LayoutId { get; set; }
    public DateTime AddedAt { get; set; }
    public Guid AddedByUserId { get; set; }

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public Layout Layout { get; set; } = null!;

    public WorkspaceLayout()
    {
        Id = Guid.NewGuid();
        AddedAt = DateTime.UtcNow;
    }
}
