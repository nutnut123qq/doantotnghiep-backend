using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Linq.Expressions;

namespace StockInvestment.Api.Controllers;

[ApiController]
[Route("api/admin/shared-layouts")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminSharedLayoutsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdminSharedLayoutsController> _logger;

    public AdminSharedLayoutsController(IUnitOfWork unitOfWork, ILogger<AdminSharedLayoutsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetSharedLayouts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? ownerId = null,
        [FromQuery] string status = "all")
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        Guid? ownerGuid = null;
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            if (!Guid.TryParse(ownerId, out var parsed))
            {
                return BadRequest("Invalid ownerId");
            }
            ownerGuid = parsed;
        }

        var now = DateTime.UtcNow;
        var normalizedStatus = status.ToLowerInvariant();
        Expression<Func<SharedLayout, bool>> predicate;

        if (ownerGuid.HasValue)
        {
            predicate = normalizedStatus switch
            {
                "active" => sl => sl.OwnerId == ownerGuid.Value && sl.ExpiresAt > now,
                "expired" => sl => sl.OwnerId == ownerGuid.Value && sl.ExpiresAt <= now,
                _ => sl => sl.OwnerId == ownerGuid.Value
            };
        }
        else
        {
            predicate = normalizedStatus switch
            {
                "active" => sl => sl.ExpiresAt > now,
                "expired" => sl => sl.ExpiresAt <= now,
                _ => sl => true
            };
        }

        var repository = _unitOfWork.Repository<SharedLayout>();
        var totalCount = await repository.CountAsync(predicate);
        var items = await repository.FindAsync(predicate);

        var pagedItems = items
            .OrderByDescending(sl => sl.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sl => new
            {
                id = sl.Id,
                code = sl.Code,
                ownerId = sl.OwnerId,
                createdAt = sl.CreatedAt,
                expiresAt = sl.ExpiresAt,
                isPublic = sl.IsPublic,
                isExpired = sl.ExpiresAt <= now
            });

        return Ok(new
        {
            items = pagedItems,
            totalCount,
            page,
            pageSize
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteSharedLayout(Guid id)
    {
        var repository = _unitOfWork.Repository<SharedLayout>();
        var layout = await repository.GetByIdAsync(id);
        if (layout == null)
        {
            return NotFound("Shared layout not found");
        }

        await repository.DeleteAsync(layout);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Admin deleted shared layout {LayoutId}", id);
        return NoContent();
    }
}

