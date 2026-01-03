using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface INotificationTemplateService
{
    Task<string> RenderTemplateAsync(Guid templateId, Dictionary<string, string> variables, CancellationToken cancellationToken = default);
    Task<bool> ValidateTemplateAsync(string template, CancellationToken cancellationToken = default);
}

