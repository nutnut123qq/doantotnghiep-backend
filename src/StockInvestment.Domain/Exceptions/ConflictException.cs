namespace StockInvestment.Domain.Exceptions;

/// <summary>
/// Exception thrown when a conflict occurs (e.g., duplicate entity)
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string entityName, string propertyName, object value)
        : base($"{entityName} with {propertyName} '{value}' already exists.")
    {
    }
}

