namespace StockInvestment.Domain.Entities;

public enum WorkspaceRole
{
    Owner = 0,
    Admin = 1,
    Member = 2
}

public class WorkspaceMember
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public WorkspaceRole Role { get; set; }
    public DateTime JoinedAt { get; set; }

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;

    public WorkspaceMember()
    {
        Id = Guid.NewGuid();
        JoinedAt = DateTime.UtcNow;
        Role = WorkspaceRole.Member;
    }
}
