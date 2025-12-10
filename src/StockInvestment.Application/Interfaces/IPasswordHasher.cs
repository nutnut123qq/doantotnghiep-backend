namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Interface for password hashing and verification
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hash a password using BCrypt
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verify a password against a hash
    /// </summary>
    bool VerifyPassword(string password, string hash);
}

