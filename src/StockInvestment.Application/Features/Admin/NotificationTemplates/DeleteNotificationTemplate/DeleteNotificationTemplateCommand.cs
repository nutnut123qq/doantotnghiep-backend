using MediatR;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.DeleteNotificationTemplate;

public class DeleteNotificationTemplateCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}

