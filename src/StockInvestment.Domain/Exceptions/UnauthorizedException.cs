namespace StockInvestment.Domain.Exceptions;

/// <summary>
/// Exception thrown when authentication fails
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public UnauthorizedException()
        : base("Authentication failed. Invalid credentials.")
    {
    }
}

