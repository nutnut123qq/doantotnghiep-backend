using System.Net.Http;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Utils;

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

        // P0-2: SSRF protection - validate URL before making request
        var urlValidation = UrlGuard.ValidateUrl(dataSource.Url);
        if (!urlValidation.IsValid)
        {
            _logger.LogWarning(
                "SSRF protection blocked URL for data source {Name} ({Id}): {Reason}",
                dataSource.Name, dataSource.Id, urlValidation.ErrorMessage);

            dataSource.Status = ConnectionStatus.Error;
            dataSource.LastChecked = DateTime.UtcNow;
            dataSource.ErrorMessage = $"URL validation failed: {urlValidation.ErrorMessage}";
            await UpdateAsync(dataSource, cancellationToken);

            throw new InvalidOperationException($"Invalid URL: {urlValidation.ErrorMessage}");
        }

        const int MaxResponseBytes = 1 * 1024 * 1024; // 1MB hard cap for TestConnection

        try
        {
            // P0-2: Use dedicated client with HttpClientHandler.MaxAutomaticRedirections = 3 (enforced in DI)
            using var httpClient = _httpClientFactory.CreateClient("DataSourceTestConnection");

            using var request = new HttpRequestMessage(HttpMethod.Get, dataSource.Url);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var isConnected = response.IsSuccessStatusCode;

            // P0-2: Hard cap response size — consume body only up to limit, then dispose
            if (response.Content != null)
            {
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > MaxResponseBytes)
                {
                    _logger.LogWarning(
                        "SSRF guard: response size {Size} exceeds limit {Limit} for {Name}",
                        contentLength.Value, MaxResponseBytes, dataSource.Name);
                    dataSource.Status = ConnectionStatus.Error;
                    dataSource.LastChecked = DateTime.UtcNow;
                    dataSource.ErrorMessage = $"Response size {contentLength.Value} exceeds allowed limit ({MaxResponseBytes} bytes)";
                    await UpdateAsync(dataSource, cancellationToken);
                    return false;
                }

                await ConsumeContentWithLimitAsync(response.Content, MaxResponseBytes, cancellationToken);
            }

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

    /// <summary>
    /// Consumes response content stream up to maxBytes to enforce SSRF response-size cap.
    /// Reads in chunks and stops once limit is reached; then disposes the stream.
    /// </summary>
    private static async Task ConsumeContentWithLimitAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[Math.Min(64 * 1024, maxBytes)];
        var totalRead = 0;
        int read;
        while (totalRead < maxBytes)
        {
            var toRead = Math.Min(buffer.Length, maxBytes - totalRead);
            read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read == 0) break;
            totalRead += read;
        }
    }
}

