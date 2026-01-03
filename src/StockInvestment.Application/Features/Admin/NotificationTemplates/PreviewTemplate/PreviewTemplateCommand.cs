using MediatR;

namespace StockInvestment.Application.Features.Admin.NotificationTemplates.PreviewTemplate;

public class PreviewTemplateCommand : IRequest<PreviewTemplateResponse>
{
    public Guid TemplateId { get; set; }
    public Dictionary<string, string> SampleData { get; set; } = new();
}

public class PreviewTemplateResponse
{
    public string RenderedSubject { get; set; } = string.Empty;
    public string RenderedBody { get; set; } = string.Empty;
}

