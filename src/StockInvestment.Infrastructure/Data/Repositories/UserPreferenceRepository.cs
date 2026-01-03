using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class UserPreferenceRepository : Repository<UserPreference>, IUserPreferenceRepository
{
    public UserPreferenceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<UserPreference>> GetByUserIdAsync(Guid userId)
    {
        return await _context.UserPreferences
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.PreferenceKey)
            .ToListAsync();
    }

    public async Task<UserPreference?> GetByUserAndKeyAsync(Guid userId, string preferenceKey)
    {
        return await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == preferenceKey);
    }

    public async Task DeleteByUserAndKeyAsync(Guid userId, string preferenceKey)
    {
        var preference = await GetByUserAndKeyAsync(userId, preferenceKey);
        if (preference != null)
        {
            _context.UserPreferences.Remove(preference);
            await _context.SaveChangesAsync();
        }
    }
}
