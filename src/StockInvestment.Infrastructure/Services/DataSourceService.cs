using System.Net.Http;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Services;

public class DataSourceService : IDataSourceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DataSourceService> _logger;

    public DataSourceService(
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        ILogger<DataSourceService> logger)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<DataSource>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.DataSources.GetAllAsync(cancellationToken);
    }

    public async Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.DataSources.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<DataSource>> GetByTypeAsync(DataSourceType type, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.DataSources.GetByTypeAsync(type, cancellationToken);
    }

    public async Task<DataSource> CreateAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        dataSource.Id = Guid.NewGuid();
        dataSource.CreatedAt = DateTime.UtcNow;
        dataSource.UpdatedAt = DateTime.UtcNow;
        dataSource.Status = ConnectionStatus.Unknown;

        await _unitOfWork.DataSources.AddAsync(dataSource, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created data source: {Name} ({Type})", dataSource.Name, dataSource.Type);

        return dataSource;
    }

    public async Task<DataSource> UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        dataSource.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.DataSources.UpdateAsync(dataSource, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated data source: {Name} ({Id})", dataSource.Name, dataSource.Id);

        return dataSource;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dataSource = await _unitOfWork.DataSources.GetByIdAsync(id, cancellationToken);
        if (dataSource == null)
        {
            throw new InvalidOperationException($"Data source with ID {id} not found");
        }

        await _unitOfWork.DataSources.DeleteAsync(dataSource, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted data source: {Name} ({Id})", dataSource.Name, dataSource.Id);
    }

    public async Task<bool> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dataSource = await _unitOfWork.DataSources.GetByIdAsync(id, cancellationToken);
        if (dataSource == null)
        {
            throw new InvalidOperationException($"Data source with ID {id} not found");
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync(dataSource.Url, cancellationToken);
            var isConnected = response.IsSuccessStatusCode;

            dataSource.Status = isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            dataSource.LastChecked = DateTime.UtcNow;
            dataSource.ErrorMessage = isConnected ? null : $"HTTP {response.StatusCode}: {response.ReasonPhrase}";

            await UpdateAsync(dataSource, cancellationToken);

            _logger.LogInformation(
                "Tested connection for data source {Name}: {Status}",
                dataSource.Name,
                dataSource.Status
            );

            return isConnected;
        }
        catch (Exception ex)
        {
            dataSource.Status = ConnectionStatus.Error;
            dataSource.LastChecked = DateTime.UtcNow;
            dataSource.ErrorMessage = ex.Message;

            await UpdateAsync(dataSource, cancellationToken);

            _logger.LogError(ex, "Error testing connection for data source {Name}", dataSource.Name);

            return false;
        }
    }
}

