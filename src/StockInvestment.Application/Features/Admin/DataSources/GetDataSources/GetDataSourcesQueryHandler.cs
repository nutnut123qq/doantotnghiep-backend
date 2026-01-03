using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.DataSources.GetDataSources;

public class GetDataSourcesQueryHandler : IRequestHandler<GetDataSourcesQuery, GetDataSourcesResponse>
{
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<GetDataSourcesQueryHandler> _logger;

    public GetDataSourcesQueryHandler(
        IDataSourceService dataSourceService,
        ILogger<GetDataSourcesQueryHandler> logger)
    {
        _dataSourceService = dataSourceService;
        _logger = logger;
    }

    public async Task<GetDataSourcesResponse> Handle(GetDataSourcesQuery request, CancellationToken cancellationToken)
    {
        var dataSources = request.Type.HasValue
            ? await _dataSourceService.GetByTypeAsync(request.Type.Value, cancellationToken)
            : await _dataSourceService.GetAllAsync(cancellationToken);

        var dtos = dataSources.Select(ds => new DataSourceDto
        {
            Id = ds.Id,
            Name = ds.Name,
            Type = ds.Type,
            Url = ds.Url,
            IsActive = ds.IsActive,
            Status = ds.Status,
            LastChecked = ds.LastChecked,
            ErrorMessage = ds.ErrorMessage,
            CreatedAt = ds.CreatedAt,
            UpdatedAt = ds.UpdatedAt,
        }).ToList();

        return new GetDataSourcesResponse { DataSources = dtos };
    }
}

