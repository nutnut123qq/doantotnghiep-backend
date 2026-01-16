using System.Text.Json;
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

    public VNStockService(
        IHttpClientFactory httpClientFactory,
        ILogger<VNStockService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("AIService");
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
                Name = s.CompanyName ?? s.Symbol,
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
        try
        {
            var url = $"/api/stock/quote/{symbol}";
            var response = await _httpClient.GetAsync(url);
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

            return new StockTicker
            {
                Symbol = data.Symbol,
                Name = data.CompanyName ?? data.Symbol,
                Exchange = ParseExchange(data.Exchange),
                Industry = data.Industry,
                CurrentPrice = data.Close,
                PreviousClose = data.Reference,
                Change = data.Change,
                ChangePercent = data.ChangePercent,
                Volume = data.Volume,
                Value = data.Value,
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("Timeout fetching quote for {Symbol}. The VNStock API may be slow or unavailable.", symbol);
            return null;
        }
        catch (HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
        {
            _logger.LogWarning("Symbol {Symbol} not found (404) in VNStock API", symbol);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching quote for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IEnumerable<StockTicker>> GetQuotesAsync(IEnumerable<string> symbols)
    {
        var tasks = symbols.Select(s => GetQuoteAsync(s));
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).Cast<StockTicker>();
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
                    Open = d.Open,
                    High = d.High,
                    Low = d.Low,
                    Close = d.Close,
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
    public string Exchange { get; set; } = string.Empty;
    public string? Industry { get; set; }
}

internal class VNStockQuoteResponse
{
    public string Symbol { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public decimal Close { get; set; }
    public decimal Reference { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public decimal Value { get; set; }
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
}

