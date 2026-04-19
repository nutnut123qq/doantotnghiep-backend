using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class PasswordResetTokenRepository : Repository<PasswordResetToken>, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.Set<PasswordResetToken>()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
    }

    public async Task InvalidateUnusedTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unused = await _context.Set<PasswordResetToken>()
            .Where(t => t.UserId == userId && !t.IsUsed)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in unused)
        {
            token.IsUsed = true;
            token.UsedAt = now;
        }
    }
}
