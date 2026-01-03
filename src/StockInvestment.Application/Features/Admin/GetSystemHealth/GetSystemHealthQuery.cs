using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetSystemHealth;

/// <summary>
/// Query to get system health status
/// </summary>
public class GetSystemHealthQuery : IRequest<SystemHealthStatus>
{
}
