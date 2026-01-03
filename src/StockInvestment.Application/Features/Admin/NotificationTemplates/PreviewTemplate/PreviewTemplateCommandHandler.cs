using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.PreviewTemplate;

public class PreviewTemplateCommandHandler : IRequestHandler<PreviewTemplateCommand, PreviewTemplateResponse>
{
    private readonly INotificationTemplateService _templateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PreviewTemplateCommandHandler> _logger;

    public PreviewTemplateCommandHandler(
        INotificationTemplateService templateService,
        IUnitOfWork unitOfWork,
        ILogger<PreviewTemplateCommandHandler> logger)
    {
        _templateService = templateService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PreviewTemplateResponse> Handle(PreviewTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _unitOfWork.Repository<Domain.Entities.NotificationTemplate>()
            .GetByIdAsync(request.TemplateId, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template with ID {request.TemplateId} not found");
        }

        var renderedBody = await _templateService.RenderTemplateAsync(
            request.TemplateId,
            request.SampleData,
            cancellationToken
        );

        var renderedSubject = template.Subject;
        foreach (var variable in request.SampleData)
        {
            renderedSubject = renderedSubject.Replace($"{{{variable.Key}}}", variable.Value);
        }

        return new PreviewTemplateResponse
        {
            RenderedSubject = renderedSubject,
            RenderedBody = renderedBody,
        };
    }
}

