namespace StockInvestment.Application.Features.Admin.Models;

public class AdminActionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdminActionResult<T> : AdminActionResult
{
    public T? Data { get; set; }
}
