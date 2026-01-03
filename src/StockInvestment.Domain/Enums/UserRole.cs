namespace StockInvestment.Domain.Enums;

/// <summary>
/// User roles for authorization
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Regular investor with basic features
    /// </summary>
    Investor = 1,
    
    /// <summary>
    /// Administrator with full system access
    /// </summary>
    Admin = 2,
    
    /// <summary>
    /// Premium user with additional features (future use)
    /// </summary>
    Premium = 3
}

