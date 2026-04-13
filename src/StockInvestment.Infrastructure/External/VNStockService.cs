using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Service to integrate with VNStock API (via Python AI Service)
/// </summary>
public class VNStockService : IVNStockService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VNStockService> _logger;
    private readonly string _aiServiceUrl;
    private const int MaxQuoteConcurrency = 2;
    private const int QuoteTimeoutSeconds = 25;
    private const int BatchQuoteTimeoutSeconds = 45;
    private const int MaxQuoteRetries = 3;
    private const decimal ThousandVndThreshold = 1_000m;
    private const decimal UnitScale = 1_000m;

    public VNStockService(
        IHttpClientFactory httpClientFactory,
        ILogger<VNStockService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("MarketDataAIService");
        _logger = logger;
        _aiServiceUrl = configuration["AIService:BaseUrl"] ?? "http://localhost:8000";
    }

    public async Task<IEnumerable<StockTicker>> GetAllSymbolsAsync(string? exchange = null)
    {
        try
        {
            var url = "/api/stock/symbols";
            if (!string.IsNullOrEmpty(exchange))
            {
                url += $"?exchange={exchange}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<VNStockSymbolResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Symbols == null)
            {
                return Enumerable.Empty<StockTicker>();
            }

            return data.Symbols.Select(s => new StockTicker
            {
                Symbol = s.Symbol,
                Name = s.CompanyName ?? s.Name ?? s.Symbol,
                Exchange = ParseExchange(s.Exchange),
                Industry = s.Industry,
                CurrentPrice = 0,
                LastUpdated = DateTime.UtcNow
            });
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("Timeout fetching symbols from VNStock API. The service may be slow or unavailable.");
            return Enumerable.Empty<StockTicker>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching symbols from VNStock");
            return Enumerable.Empty<StockTicker>();
        }
    }

    public async Task<StockTicker?> GetQuoteAsync(string symbol)
    {
        for (var attempt = 0; attempt <= MaxQuoteRetries; attempt++)
        {
            try
            {
                var url = $"/api/stock/quote/{symbol}";
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(QuoteTimeoutSeconds));
                var response = await _httpClient.GetAsync(url, cts.Token);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<VNStockQuoteResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data == null)
                {
                    return null;
                }

                return MapQuoteResponseToTicker(data);
            }
            catch (TaskCanceledException)
            {
                // Retry transient transport issues a few times with a short backoff.
                if (attempt < MaxQuoteRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(450 * (attempt + 1)));
                    continue;
                }

                _logger.LogWarning("Timeout/transport error fetching quote for {Symbol}. The VNStock API may be slow or unavailable.", symbol);
                return null;
            }
            catch (HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
            {
                _logger.LogWarning("Symbol {Symbol} not found (404) in VNStock API", symbol);
                return null;
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MaxQuoteRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * (attempt + 1)));
                    continue;
                }

                _logger.LogError(ex, "HTTP error fetching quote for {Symbol}", symbol);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching quote for {Symbol}", symbol);
                return null;
            }
        }

        return null;
    }

    public async Task<IEnumerable<StockTicker>> GetQuotesAsync(IEnumerable<string> symbols)
    {
        var uniqueSymbols = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (!uniqueSymbols.Any())
        {
            return Enumerable.Empty<StockTicker>();
        }

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var url = "/api/stock/quotes?source=VCI";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(BatchQuoteTimeoutSeconds));
            var response = await _httpClient.PostAsJsonAsync(url, uniqueSymbols, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Batch quotes POST failed with {StatusCode}; falling back to per-symbol quotes.",
                    (int)response.StatusCode);
                return await GetQuotesParallelFallbackAsync(uniqueSymbols);
            }

            var list = await response.Content.ReadFromJsonAsync<List<VNStockQuoteResponse>>(jsonOptions, cts.Token);
            if (list == null || list.Count == 0)
            {
                _logger.LogWarning("Batch quotes returned empty; falling back to per-symbol quotes.");
                return await GetQuotesParallelFallbackAsync(uniqueSymbols);
            }

            return list.Select(MapQuoteResponseToTicker);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch quotes request failed; falling back to per-symbol quotes.");
            return await GetQuotesParallelFallbackAsync(uniqueSymbols);
        }
    }

    private async Task<IEnumerable<StockTicker>> GetQuotesParallelFallbackAsync(IReadOnlyList<string> uniqueSymbols)
    {
        var throttler = new SemaphoreSlim(MaxQuoteConcurrency);
        try
        {
            var tasks = uniqueSymbols.Select(async symbol =>
            {
                await throttler.WaitAsync();
                try
                {
                    return await GetQuoteAsync(symbol);
                }
                finally
                {
                    throttler.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).Cast<StockTicker>();
        }
        finally
        {
            throttler.Dispose();
        }
    }

    public async Task<IEnumerable<OHLCVData>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        try
        {
            var url = $"/api/stock/history/{symbol}?start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var dataList = JsonSerializer.Deserialize<List<VNStockHistoricalData>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dataList == null || dataList.Count == 0)
            {
                return Enumerable.Empty<OHLCVData>();
            }

            var samplePrice = dataList
                .SelectMany(d => new[] { d.Close, d.Open, d.High, d.Low })
                .FirstOrDefault(p => p > 0);
            var sampleUnit = dataList.Select(d => d.PriceUnit).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            var historyScale = ResolveScale(samplePrice, sampleUnit);

            return dataList.Select(d =>
            {
                DateTime date;
                if (!string.IsNullOrEmpty(d.Time))
                {
                    // Parse time string to DateTime
                    if (DateTime.TryParse(d.Time, out var parsedDate))
                    {
                        date = parsedDate;
                    }
                    else if (long.TryParse(d.Time, out var timestamp))
                    {
                        // Handle Unix timestamp
                        date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    }
                    else
                    {
                        date = d.Date != default ? d.Date : DateTime.UtcNow;
                    }
                }
                else
                {
                    date = d.Date != default ? d.Date : DateTime.UtcNow;
                }

                return new OHLCVData
                {
                    Date = date,
                    Open = NormalizePrice(d.Open, historyScale),
                    High = NormalizePrice(d.High, historyScale),
                    Low = NormalizePrice(d.Low, historyScale),
                    Close = NormalizePrice(d.Close, historyScale),
                    Volume = d.Volume
                };
            });
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("Timeout fetching historical data for {Symbol}. The VNStock API may be slow or unavailable.", symbol);
            return Enumerable.Empty<OHLCVData>();
        }
        catch (HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
        {
            _logger.LogWarning("Historical data not found (404) for {Symbol} in VNStock API", symbol);
            return Enumerable.Empty<OHLCVData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical data for {Symbol}", symbol);
            return Enumerable.Empty<OHLCVData>();
        }
    }

    private Exchange ParseExchange(string exchange)
    {
        return exchange?.ToUpper() switch
        {
            "HOSE" => Exchange.HOSE,
            "HNX" => Exchange.HNX,
            "UPCOM" => Exchange.UPCOM,
            _ => Exchange.HOSE
        };
    }

    /// <summary>
    /// Maps AI service (FastAPI) quote JSON to domain ticker. Python returns currentPrice/previousClose;
    /// older payloads may use close/reference.
    /// </summary>
    private StockTicker MapQuoteResponseToTicker(VNStockQuoteResponse data)
    {
        var rawPrice = data.CurrentPrice ?? (data.Close != 0 ? data.Close : 0m);
        var previous =
            data.PreviousClose
            ?? (data.Reference != 0 ? data.Reference : (decimal?)null);
        var scale = ResolveScale(rawPrice, data.PriceUnit);
        var price = NormalizePrice(rawPrice, scale);
        var normalizedPrevious = NormalizeNullablePrice(previous, scale);
        var normalizedChange = NormalizeNullablePrice(data.Change, scale);

        decimal? value = data.Value;
        if (value is > 0 && scale != 1m)
        {
            value *= scale;
        }

        if ((value is null || value == 0) && price > 0 && data.Volume > 0)
        {
            value = price * data.Volume;
        }

        return new StockTicker
        {
            Symbol = data.Symbol,
            Name = data.CompanyName ?? data.Name ?? data.Symbol,
            Exchange = ParseExchange(data.Exchange),
            Industry = data.Industry,
            CurrentPrice = price,
            PreviousClose = normalizedPrevious,
            Change = normalizedChange,
            ChangePercent = data.ChangePercent,
            Volume = data.Volume,
            Value = value,
            LastUpdated = DateTime.UtcNow
        };
    }

    private static decimal ResolveScale(decimal rawPrice, string? priceUnit)
    {
        if (!string.IsNullOrWhiteSpace(priceUnit))
        {
            var normalizedUnit = priceUnit.Trim().ToUpperInvariant();
            if (normalizedUnit is "THOUSAND_VND" or "K_VND")
            {
                return UnitScale;
            }

            // Python pipeline returns full VND with priceUnit VND; avoid double-scaling on small raw values.
            if (normalizedUnit is "VND")
            {
                return 1m;
            }
        }

        if (rawPrice > 0 && rawPrice < ThousandVndThreshold)
        {
            return UnitScale;
        }

        return 1m;
    }

    private static decimal NormalizePrice(decimal value, decimal scale)
    {
        if (value <= 0)
        {
            return value;
        }

        return value * scale;
    }

    private static decimal? NormalizeNullablePrice(decimal? value, decimal scale)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return NormalizePrice(value.Value, scale);
    }
}

// Response DTOs for VNStock API
internal class VNStockSymbolResponse
{
    public List<VNStockSymbol> Symbols { get; set; } = new();
}

internal class VNStockSymbol
{
    public string Symbol { get; set; } = string.Empty;
    public string? CompanyName { get; set; }

    /// <summary>FastAPI SymbolInfo uses "name".</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    public string Exchange { get; set; } = string.Empty;
    public string? Industry { get; set; }
}

internal class VNStockQuoteResponse
{
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    public string Exchange { get; set; } = string.Empty;
    public string? Industry { get; set; }

    [JsonPropertyName("currentPrice")]
    public decimal? CurrentPrice { get; set; }

    /// <summary>Legacy/alternate quote shape.</summary>
    public decimal Close { get; set; }

    [JsonPropertyName("previousClose")]
    public decimal? PreviousClose { get; set; }

    public decimal Reference { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }

    [JsonPropertyName("priceUnit")]
    public string? PriceUnit { get; set; }
}

internal class VNStockHistoricalResponse
{
    public List<VNStockHistoricalData> Data { get; set; } = new();
}

internal class VNStockHistoricalData
{
    [System.Text.Json.Serialization.JsonPropertyName("time")]
    public string? Time { get; set; }
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }

    [JsonPropertyName("priceUnit")]
    public string? PriceUnit { get; set; }
}

