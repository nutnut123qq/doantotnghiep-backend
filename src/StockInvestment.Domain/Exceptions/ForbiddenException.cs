namespace StockInvestment.Domain.Exceptions;

/// <summary>
/// Exception thrown when user doesn't have permission to access a resource
/// </summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException()
        : base("You don't have permission to access this resource.")
    {
    }
}

