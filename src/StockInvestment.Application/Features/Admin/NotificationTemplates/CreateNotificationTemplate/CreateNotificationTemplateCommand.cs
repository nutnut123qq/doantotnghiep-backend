using MediatR;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.CreateNotificationTemplate;

public class CreateNotificationTemplateCommand : IRequest<NotificationTemplateDto>
{
    public string Name { get; set; } = string.Empty;
    public NotificationEventType EventType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NotificationEventType EventType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

