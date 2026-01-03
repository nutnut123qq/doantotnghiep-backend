using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.DataSources.TestConnection;

public class TestConnectionCommandHandler : IRequestHandler<TestConnectionCommand, TestConnectionResponse>
{
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<TestConnectionCommandHandler> _logger;

    public TestConnectionCommandHandler(
        IDataSourceService dataSourceService,
        ILogger<TestConnectionCommandHandler> logger)
    {
        _dataSourceService = dataSourceService;
        _logger = logger;
    }

    public async Task<TestConnectionResponse> Handle(TestConnectionCommand request, CancellationToken cancellationToken)
    {
        var isConnected = await _dataSourceService.TestConnectionAsync(request.Id, cancellationToken);
        
        var dataSource = await _dataSourceService.GetByIdAsync(request.Id, cancellationToken);
        
        return new TestConnectionResponse
        {
            IsConnected = isConnected,
            ErrorMessage = dataSource?.ErrorMessage,
            LastChecked = dataSource?.LastChecked ?? DateTime.UtcNow,
        };
    }
}

