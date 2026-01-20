namespace StockInvestment.Domain.Entities;

/// <summary>
/// Audit log for admin actions on users
/// </summary>
public class AdminAuditLog
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }

    public AdminAuditLog()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}
