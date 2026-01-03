using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetSystemStats;

public class GetSystemStatsQueryHandler : IRequestHandler<GetSystemStatsQuery, SystemStats>
{
    private readonly IAdminService _adminService;

    public GetSystemStatsQueryHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public async Task<SystemStats> Handle(GetSystemStatsQuery request, CancellationToken cancellationToken)
    {
        return await _adminService.GetSystemStatsAsync();
    }
}
