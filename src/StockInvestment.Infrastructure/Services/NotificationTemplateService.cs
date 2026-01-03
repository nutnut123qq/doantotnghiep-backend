using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class NotificationTemplateService : INotificationTemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationTemplateService> _logger;

    public NotificationTemplateService(
        IUnitOfWork unitOfWork,
        ILogger<NotificationTemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<string> RenderTemplateAsync(Guid templateId, Dictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.Repository<NotificationTemplate>()
            .GetByIdAsync(templateId, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template with ID {templateId} not found");
        }

        var rendered = template.Body;
        foreach (var variable in variables)
        {
            rendered = rendered.Replace($"{{{variable.Key}}}", variable.Value);
        }

        return rendered;
    }

    public Task<bool> ValidateTemplateAsync(string template, CancellationToken cancellationToken = default)
    {
        // Simple validation: check for balanced braces
        var openBraces = template.Count(c => c == '{');
        var closeBraces = template.Count(c => c == '}');
        
        return Task.FromResult(openBraces == closeBraces);
    }
}

