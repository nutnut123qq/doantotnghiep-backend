using MediatR;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.DataSources.UpdateDataSource;

public class UpdateDataSourceCommand : IRequest<DataSourceDto>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DataSourceType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; }
    public string? Config { get; set; }
}

public class DataSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DataSourceType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public ConnectionStatus Status { get; set; }
    public DateTime? LastChecked { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

