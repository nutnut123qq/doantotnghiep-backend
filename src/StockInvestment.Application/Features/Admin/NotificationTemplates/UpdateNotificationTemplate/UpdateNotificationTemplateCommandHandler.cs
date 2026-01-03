using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.UpdateNotificationTemplate;

public class UpdateNotificationTemplateCommandHandler : IRequestHandler<UpdateNotificationTemplateCommand, NotificationTemplateDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateNotificationTemplateCommandHandler> _logger;

    public UpdateNotificationTemplateCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateNotificationTemplateCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationTemplateDto> Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.Repository<NotificationTemplate>()
            .GetByIdAsync(request.Id, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template with ID {request.Id} not found");
        }

        template.Name = request.Name;
        template.EventType = request.EventType;
        template.Subject = request.Subject;
        template.Body = request.Body;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<NotificationTemplate>().UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated notification template: {Name} ({Id})", template.Name, template.Id);

        return new NotificationTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            EventType = template.EventType,
            Subject = template.Subject,
            Body = template.Body,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
        };
    }
}

