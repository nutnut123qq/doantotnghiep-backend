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
        const int MaxRedirects = 3; // P0-2: Maximum redirects allowed

        try
        {
            // P0-2: Use dedicated client with auto-redirect DISABLED - we'll manually follow redirects
            using var httpClient = _httpClientFactory.CreateClient("DataSourceTestConnection");

            var currentUrl = dataSource.Url;
            var redirectCount = 0;
            HttpResponseMessage? finalResponse = null;

            // P0-2: Manually follow redirects with validation at each step
            while (redirectCount <= MaxRedirects)
            {
                // Validate current URL before making request
                var urlValidation = UrlGuard.ValidateUrl(currentUrl);
                if (!urlValidation.IsValid)
                {
                    _logger.LogWarning(
                        "SSRF protection blocked redirect URL for data source {Name} ({Id}) at step {Step}: {Reason}",
                        dataSource.Name, dataSource.Id, redirectCount, urlValidation.ErrorMessage);
                    
                    dataSource.Status = ConnectionStatus.Error;
                    dataSource.LastChecked = DateTime.UtcNow;
                    dataSource.ErrorMessage = $"Redirect validation failed at step {redirectCount}: {urlValidation.ErrorMessage}";
                    await UpdateAsync(dataSource, cancellationToken);
                    
                    throw new InvalidOperationException($"Invalid redirect URL: {urlValidation.ErrorMessage}");
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                // If not a redirect, use this as final response
                if (!IsRedirectResponse(response.StatusCode))
                {
                    finalResponse = response;
                    break;
                }

                // Check redirect count limit
                if (redirectCount >= MaxRedirects)
                {
                    _logger.LogWarning(
                        "SSRF protection: Maximum redirects ({MaxRedirects}) exceeded for data source {Name} ({Id})",
                        MaxRedirects, dataSource.Name, dataSource.Id);
                    
                    dataSource.Status = ConnectionStatus.Error;
                    dataSource.LastChecked = DateTime.UtcNow;
                    dataSource.ErrorMessage = $"Maximum redirects ({MaxRedirects}) exceeded";
                    await UpdateAsync(dataSource, cancellationToken);
                    
                    response.Dispose();
                    return false;
                }

                // Get redirect location
                var location = response.Headers.Location?.ToString();
                if (string.IsNullOrWhiteSpace(location))
                {
                    // No Location header, use this response as final
                    finalResponse = response;
                    break;
                }

                // Resolve relative URLs
                if (Uri.TryCreate(new Uri(currentUrl), location, out var redirectUri))
                {
                    currentUrl = redirectUri.ToString();
                    redirectCount++;
                    _logger.LogDebug(
                        "Following redirect {Count}/{Max} for data source {Name}: {From} -> {To}",
                        redirectCount, MaxRedirects, dataSource.Name, dataSource.Url, currentUrl);
                    
                    // Dispose response before next iteration
                    response.Dispose();
                }
                else
                {
                    _logger.LogWarning(
                        "Invalid redirect location '{Location}' for data source {Name} ({Id})",
                        location, dataSource.Name, dataSource.Id);
                    // Use this response as final
                    finalResponse = response;
                    break;
                }
            }

            if (finalResponse == null)
            {
                throw new InvalidOperationException("Failed to get response");
            }

            var isConnected = finalResponse.IsSuccessStatusCode;

            // P0-2: Hard cap response size — consume body only up to limit, then dispose
            if (finalResponse.Content != null)
            {
                var contentLength = finalResponse.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > MaxResponseBytes)
                {
                    _logger.LogWarning(
                        "SSRF guard: response size {Size} exceeds limit {Limit} for {Name}",
                        contentLength.Value, MaxResponseBytes, dataSource.Name);
                    dataSource.Status = ConnectionStatus.Error;
                    dataSource.LastChecked = DateTime.UtcNow;
                    dataSource.ErrorMessage = $"Response size {contentLength.Value} exceeds allowed limit ({MaxResponseBytes} bytes)";
                    await UpdateAsync(dataSource, cancellationToken);
                    finalResponse.Dispose();
                    return false;
                }

                await ConsumeContentWithLimitAsync(finalResponse.Content, MaxResponseBytes, cancellationToken);
            }

            dataSource.Status = isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            dataSource.LastChecked = DateTime.UtcNow;
            dataSource.ErrorMessage = isConnected ? null : $"HTTP {finalResponse.StatusCode}: {finalResponse.ReasonPhrase}";
            
            finalResponse.Dispose();

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
    /// P0-2: Check if HTTP status code indicates a redirect
    /// </summary>
    private static bool IsRedirectResponse(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.MovedPermanently ||
               statusCode == System.Net.HttpStatusCode.Found ||
               statusCode == System.Net.HttpStatusCode.SeeOther ||
               statusCode == System.Net.HttpStatusCode.TemporaryRedirect ||
               statusCode == System.Net.HttpStatusCode.PermanentRedirect ||
               (int)statusCode == 308; // Permanent Redirect
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

