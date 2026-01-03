using MediatR;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.DataSources.GetDataSources;

public class GetDataSourcesQuery : IRequest<GetDataSourcesResponse>
{
    public DataSourceType? Type { get; set; }
}

public class GetDataSourcesResponse
{
    public List<DataSourceDto> DataSources { get; set; } = new();
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

