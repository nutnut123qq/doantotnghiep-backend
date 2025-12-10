using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Application.Specifications;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.ValueObjects;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Data.Repositories;

/// <summary>
/// User repository with custom methods, inheriting from generic repository
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Get user by email - using direct query to avoid EF Core translation issues with Value Objects
    /// </summary>
    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        // Query directly using the stored email value in the database
        // Use FromSqlRaw to bypass EF Core's value converter issues
        var emailValue = email.Value;
        var dbSet = _context.Set<User>();
        return await dbSet
            .FromSqlRaw("SELECT * FROM \"Users\" WHERE \"Email\" = {0}", emailValue)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

