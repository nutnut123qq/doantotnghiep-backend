using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Repository for email verification tokens
/// </summary>
public interface IEmailVerificationTokenRepository : IRepository<EmailVerificationToken>
{
    /// <summary>
    /// Get token by token string
    /// </summary>
    Task<EmailVerificationToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active token for user
    /// </summary>
    Task<EmailVerificationToken?> GetActiveTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark every unused verification token for the user as used (no persistence — caller saves).
    /// </summary>
    Task InvalidateUnusedTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
