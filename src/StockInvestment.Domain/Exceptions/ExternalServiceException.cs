namespace StockInvestment.Domain.Exceptions;

/// <summary>
/// Exception thrown when an external service call fails
/// </summary>
public class ExternalServiceException : DomainException
{
    public string ServiceName { get; }
    public int? StatusCode { get; }

    public ExternalServiceException(string serviceName, string message)
        : base(message)
    {
        ServiceName = serviceName;
    }

    public ExternalServiceException(string serviceName, string message, Exception innerException)
        : base(message, innerException)
    {
        ServiceName = serviceName;
        
        // Try to extract status code from HttpRequestException
        if (innerException is HttpRequestException httpEx && httpEx.Data.Contains("StatusCode"))
        {
            StatusCode = (int)httpEx.Data["StatusCode"]!;
        }
    }

    public ExternalServiceException(string serviceName, string message, int statusCode)
        : base(message)
    {
        ServiceName = serviceName;
        StatusCode = statusCode;
    }
}
