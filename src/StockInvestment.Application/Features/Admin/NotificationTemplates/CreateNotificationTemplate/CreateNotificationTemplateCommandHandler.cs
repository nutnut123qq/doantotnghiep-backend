using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.CreateNotificationTemplate;

public class CreateNotificationTemplateCommandHandler : IRequestHandler<CreateNotificationTemplateCommand, NotificationTemplateDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateNotificationTemplateCommandHandler> _logger;

    public CreateNotificationTemplateCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateNotificationTemplateCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationTemplateDto> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            EventType = request.EventType,
            Subject = request.Subject,
            Body = request.Body,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _unitOfWork.Repository<NotificationTemplate>().AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created notification template: {Name} ({Id})", template.Name, template.Id);

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

