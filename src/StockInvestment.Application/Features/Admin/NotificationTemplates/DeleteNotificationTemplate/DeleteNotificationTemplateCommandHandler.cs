using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.DeleteNotificationTemplate;

public class DeleteNotificationTemplateCommandHandler : IRequestHandler<DeleteNotificationTemplateCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteNotificationTemplateCommandHandler> _logger;

    public DeleteNotificationTemplateCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeleteNotificationTemplateCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.Repository<NotificationTemplate>()
            .GetByIdAsync(request.Id, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template with ID {request.Id} not found");
        }

        await _unitOfWork.Repository<NotificationTemplate>().DeleteAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted notification template: {Name} ({Id})", template.Name, template.Id);

        return true;
    }
}

