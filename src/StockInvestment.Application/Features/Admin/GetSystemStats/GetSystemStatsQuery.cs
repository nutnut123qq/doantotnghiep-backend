using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetSystemStats;

/// <summary>
/// Query to get system statistics
/// </summary>
public class GetSystemStatsQuery : IRequest<SystemStats>
{
}
