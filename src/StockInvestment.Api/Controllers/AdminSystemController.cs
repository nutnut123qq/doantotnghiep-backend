using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Features.Admin.GetAnalytics;
using StockInvestment.Application.Features.Admin.GetEndpointMetrics;
using StockInvestment.Application.Features.Admin.GetPopularStocks;
using StockInvestment.Application.Features.Admin.GetSystemHealth;
using StockInvestment.Application.Features.Admin.GetSystemStats;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminSystemController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminSystemController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stats")]
    public async Task<ActionResult> GetSystemStats() => Ok(await _mediator.Send(new GetSystemStatsQuery()));

    [HttpGet("health")]
    public async Task<ActionResult> GetSystemHealth() => Ok(await _mediator.Send(new GetSystemHealthQuery()));

    [HttpGet("analytics")]
    public async Task<ActionResult> GetAnalytics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        => Ok(await _mediator.Send(new GetAnalyticsQuery { StartDate = startDate, EndDate = endDate }));

    [HttpGet("popular-stocks")]
    public async Task<ActionResult> GetPopularStocks([FromQuery] int topN = 10, [FromQuery] int daysBack = 7)
        => Ok(await _mediator.Send(new GetPopularStocksQuery { TopN = topN, DaysBack = daysBack }));

    [HttpGet("endpoint-metrics")]
    public async Task<ActionResult> GetEndpointMetrics([FromQuery] int topN = 20)
        => Ok(await _mediator.Send(new GetEndpointMetricsQuery { TopN = topN }));
}
