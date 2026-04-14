using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class CorporateEventRepository : ICorporateEventRepository
{
    private readonly ApplicationDbContext _context;

    public CorporateEventRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CorporateEvent>> GetAllAsync(
        string? symbol = null,
        CorporateEventType? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        EventStatus? status = null)
    {
        var query = _context.CorporateEvents
            .Include(e => e.StockTicker)
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(symbol))
        {
            query = query.Where(e => e.StockTicker.Symbol == symbol);
        }

        if (eventType.HasValue)
        {
            query = query.Where(e => e.EventType == eventType.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(e => e.EventDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.EventDate <= endDate.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        return await query
            .OrderBy(e => e.EventDate)
            .ToListAsync();
    }

    public async Task<CorporateEvent?> GetByIdAsync(Guid id)
    {
        return await _context.CorporateEvents
            .Include(e => e.StockTicker)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }

    public async Task<IEnumerable<CorporateEvent>> GetByTickerAsync(Guid stockTickerId)
    {
        return await _context.CorporateEvents
            .Include(e => e.StockTicker)
            .Where(e => e.StockTickerId == stockTickerId && !e.IsDeleted)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CorporateEvent>> GetUpcomingEventsAsync(int daysAhead = 30)
    {
        var today = DateTime.UtcNow.Date;
        var futureDate = today.AddDays(daysAhead);

        return await _context.CorporateEvents
            .Include(e => e.StockTicker)
            .Where(e => !e.IsDeleted && e.EventDate >= today && e.EventDate <= futureDate && e.Status == EventStatus.Upcoming)
            .OrderBy(e => e.EventDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CorporateEvent>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.CorporateEvents
            .Include(e => e.StockTicker)
            .Where(e => !e.IsDeleted && e.EventDate >= startDate && e.EventDate <= endDate)
            .OrderBy(e => e.EventDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CorporateEvent>> GetByTypeAsync(CorporateEventType eventType)
    {
        return await _context.CorporateEvents
            .Include(e => e.StockTicker)
            .Where(e => !e.IsDeleted && e.EventType == eventType)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync();
    }

    public async Task<CorporateEvent> CreateAsync(CorporateEvent corporateEvent)
    {
        _context.CorporateEvents.Add(corporateEvent);
        await _context.SaveChangesAsync();
        return corporateEvent;
    }

    public async Task UpdateAsync(CorporateEvent corporateEvent)
    {
        corporateEvent.UpdatedAt = DateTime.UtcNow;
        _context.CorporateEvents.Update(corporateEvent);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var corporateEvent = await _context.CorporateEvents.FindAsync(id);
        if (corporateEvent != null)
        {
            _context.CorporateEvents.Remove(corporateEvent);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid stockTickerId, CorporateEventType eventType, DateTime eventDate)
    {
        return await _context.CorporateEvents
            .AnyAsync(e => 
                !e.IsDeleted &&
                e.StockTickerId == stockTickerId && 
                e.EventType == eventType && 
                e.EventDate.Date == eventDate.Date);
    }

    public async Task<bool> ExistsBySourceUrlAsync(string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return false;

        return await _context.CorporateEvents
            .AnyAsync(e => !e.IsDeleted && e.SourceUrl != null && e.SourceUrl.ToLower() == sourceUrl.ToLower());
    }

    public async Task<IReadOnlyList<CorporateEvent>> GetRecentBySymbolAsync(string symbol, DateTime sinceUtc, int take)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        var cap = Math.Clamp(take, 1, 100);

        return await _context.CorporateEvents
            .Include(e => e.StockTicker)
            .Where(e => !e.IsDeleted && e.StockTicker.Symbol == normalized && e.EventDate >= sinceUtc.Date)
            .OrderByDescending(e => e.EventDate)
            .Take(cap)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<CorporateEvent> Items, int TotalCount)> GetForAdminAsync(
        int page = 1,
        int pageSize = 20,
        string? symbol = null,
        CorporateEventType? eventType = null,
        EventStatus? status = null)
    {
        var query = _context.CorporateEvents
            .Include(e => e.StockTicker)
            .AsQueryable();

        var normalizedSymbol = symbol?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            query = query.Where(e => e.StockTicker.Symbol == normalizedSymbol);
        }

        if (eventType.HasValue)
        {
            query = query.Where(e => e.EventType == eventType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.EventDate)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public async Task<bool> SetDeletedAsync(Guid id, bool isDeleted)
    {
        var row = await _context.CorporateEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (row == null)
        {
            return false;
        }

        if (row.IsDeleted == isDeleted)
        {
            return true;
        }

        row.IsDeleted = isDeleted;
        row.UpdatedAt = DateTime.UtcNow;
        _context.CorporateEvents.Update(row);
        await _context.SaveChangesAsync();
        return true;
    }
}
