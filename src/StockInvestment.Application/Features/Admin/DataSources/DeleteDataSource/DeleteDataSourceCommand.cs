using MediatR;

namespace StockInvestment.Application.Features.Admin.DataSources.DeleteDataSource;

public class DeleteDataSourceCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}

