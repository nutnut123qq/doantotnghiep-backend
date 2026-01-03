using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.DataSources.UpdateDataSource;

public class UpdateDataSourceCommandHandler : IRequestHandler<UpdateDataSourceCommand, DataSourceDto>
{
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<UpdateDataSourceCommandHandler> _logger;

    public UpdateDataSourceCommandHandler(
        IDataSourceService dataSourceService,
        ILogger<UpdateDataSourceCommandHandler> logger)
    {
        _dataSourceService = dataSourceService;
        _logger = logger;
    }

    public async Task<DataSourceDto> Handle(UpdateDataSourceCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dataSourceService.GetByIdAsync(request.Id, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Data source with ID {request.Id} not found");
        }

        existing.Name = request.Name;
        existing.Type = request.Type;
        existing.Url = request.Url;
        existing.ApiKey = request.ApiKey;
        existing.IsActive = request.IsActive;
        existing.Config = request.Config;

        var updated = await _dataSourceService.UpdateAsync(existing, cancellationToken);

        return new DataSourceDto
        {
            Id = updated.Id,
            Name = updated.Name,
            Type = updated.Type,
            Url = updated.Url,
            IsActive = updated.IsActive,
            Status = updated.Status,
            LastChecked = updated.LastChecked,
            ErrorMessage = updated.ErrorMessage,
            CreatedAt = updated.CreatedAt,
            UpdatedAt = updated.UpdatedAt,
        };
    }
}

