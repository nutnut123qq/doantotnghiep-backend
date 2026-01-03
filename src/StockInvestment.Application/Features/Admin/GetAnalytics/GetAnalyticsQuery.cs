using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.GetAnalytics;

/// <summary>
/// Query to get system analytics
/// </summary>
public class GetAnalyticsQuery : IRequest<ApiAnalytics>
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
