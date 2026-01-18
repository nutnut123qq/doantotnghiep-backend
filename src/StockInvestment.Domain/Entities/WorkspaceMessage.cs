namespace StockInvestment.Domain.Entities;

public class WorkspaceMessage
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;

    public WorkspaceMessage()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}
