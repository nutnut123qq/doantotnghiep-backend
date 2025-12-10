using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// BCrypt-based password hasher implementation
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Hash a password using BCrypt with work factor 12
    /// </summary>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// Verify a password against a BCrypt hash
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // If hash is invalid format, return false
            return false;
        }
    }
}

