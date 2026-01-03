using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.DataSources.CreateDataSource;

public class CreateDataSourceCommandHandler : IRequestHandler<CreateDataSourceCommand, DataSourceDto>
{
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<CreateDataSourceCommandHandler> _logger;

    public CreateDataSourceCommandHandler(
        IDataSourceService dataSourceService,
        ILogger<CreateDataSourceCommandHandler> logger)
    {
        _dataSourceService = dataSourceService;
        _logger = logger;
    }

    public async Task<DataSourceDto> Handle(CreateDataSourceCommand request, CancellationToken cancellationToken)
    {
        var dataSource = new DataSource
        {
            Name = request.Name,
            Type = request.Type,
            Url = request.Url,
            ApiKey = request.ApiKey,
            IsActive = request.IsActive,
            Config = request.Config,
        };

        var created = await _dataSourceService.CreateAsync(dataSource, cancellationToken);

        return new DataSourceDto
        {
            Id = created.Id,
            Name = created.Name,
            Type = created.Type,
            Url = created.Url,
            IsActive = created.IsActive,
            Status = created.Status,
            LastChecked = created.LastChecked,
            ErrorMessage = created.ErrorMessage,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt,
        };
    }
}

