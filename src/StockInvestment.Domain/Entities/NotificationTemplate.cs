using StockInvestment.Domain.Enums;

namespace StockInvestment.Domain.Entities;

public class NotificationTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NotificationEventType EventType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty; // Support variables: {stockSymbol}, {price}, etc.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

