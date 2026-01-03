using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetSystemHealth;

public class GetSystemHealthQueryHandler : IRequestHandler<GetSystemHealthQuery, SystemHealthStatus>
{
    private readonly ISystemHealthService _systemHealthService;

    public GetSystemHealthQueryHandler(ISystemHealthService systemHealthService)
    {
        _systemHealthService = systemHealthService;
    }

    public async Task<SystemHealthStatus> Handle(GetSystemHealthQuery request, CancellationToken cancellationToken)
    {
        return await _systemHealthService.GetSystemHealthAsync();
    }
}
