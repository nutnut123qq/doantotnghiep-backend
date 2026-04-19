using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Repository for password reset tokens (self-service "forgot password" flow).
/// </summary>
public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    /// <summary>
    /// Get token by its string value (includes the associated User).
    /// </summary>
    Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark every unused reset token for the user as used. No persistence —
    /// the caller is expected to commit via UnitOfWork.
    /// </summary>
    Task InvalidateUnusedTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
