using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.GetNotificationTemplates;

public class GetNotificationTemplatesQueryHandler : IRequestHandler<GetNotificationTemplatesQuery, GetNotificationTemplatesResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetNotificationTemplatesQueryHandler> _logger;

    public GetNotificationTemplatesQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetNotificationTemplatesQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GetNotificationTemplatesResponse> Handle(GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = request.EventType.HasValue
            ? await _unitOfWork.Repository<NotificationTemplate>()
                .GetAllAsync(cancellationToken)
                .ContinueWith(t => t.Result.Where(tmpl => tmpl.EventType == request.EventType.Value), cancellationToken)
            : await _unitOfWork.Repository<NotificationTemplate>().GetAllAsync(cancellationToken);

        var dtos = templates.Select(t => new NotificationTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            EventType = t.EventType,
            Subject = t.Subject,
            Body = t.Body,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
        }).ToList();

        return new GetNotificationTemplatesResponse { Templates = dtos };
    }
}

