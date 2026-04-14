using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Api.Contracts.Responses;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/admin/financial-reports")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminFinancialReportsController : ControllerBase
{
    private readonly IFinancialReportService _financialReportService;

    public AdminFinancialReportsController(IFinancialReportService financialReportService)
    {
        _financialReportService = financialReportService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? symbol = null)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _financialReportService.GetReportsForAdminAsync(safePage, safePageSize, symbol);
        var items = result.Items.Select(r => new
        {
            r.Id,
            r.TickerId,
            symbol = r.Ticker?.Symbol,
            r.ReportType,
            r.Year,
            r.Quarter,
            r.ReportDate,
            r.CreatedAt,
            r.IsDeleted
        }).Cast<object>().ToList();
        return Ok(new PagedResponse<object>
        {
            Items = items,
            TotalCount = result.TotalCount,
            PageNumber = safePage,
            PageSize = safePageSize
        });
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetReportDeleted(Guid id, [FromBody] SetFinancialReportDeletedRequest? body)
    {
        if (body == null)
        {
            return BadRequest();
        }

        var updated = await _financialReportService.SetReportDeletedAsync(id, body.IsDeleted);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }
}

public class SetFinancialReportDeletedRequest
{
    public bool IsDeleted { get; set; }
}
