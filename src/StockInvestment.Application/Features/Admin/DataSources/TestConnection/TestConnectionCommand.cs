using MediatR;

namespace StockInvestment.Application.Features.Admin.DataSources.TestConnection;

public class TestConnectionCommand : IRequest<TestConnectionResponse>
{
    public Guid Id { get; set; }
}

public class TestConnectionResponse
{
    public bool IsConnected { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastChecked { get; set; }
}

