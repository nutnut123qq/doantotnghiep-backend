using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.DataSources.DeleteDataSource;

public class DeleteDataSourceCommandHandler : IRequestHandler<DeleteDataSourceCommand, bool>
{
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<DeleteDataSourceCommandHandler> _logger;

    public DeleteDataSourceCommandHandler(
        IDataSourceService dataSourceService,
        ILogger<DeleteDataSourceCommandHandler> logger)
    {
        _dataSourceService = dataSourceService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteDataSourceCommand request, CancellationToken cancellationToken)
    {
        await _dataSourceService.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}

