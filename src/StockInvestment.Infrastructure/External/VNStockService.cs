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
            var url = $"{_aiServiceUrl}/api/stock/symbols";
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
            var url = $"{_aiServiceUrl}/api/stock/quote/{symbol}";
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
            var url = $"{_aiServiceUrl}/api/stock/historical/{symbol}?start={startDate:yyyy-MM-dd}&end={endDate:yyyy-MM-dd}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<VNStockHistoricalResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Data == null)
            {
                return Enumerable.Empty<OHLCVData>();
            }

            return data.Data.Select(d => new OHLCVData
            {
                Date = d.Date,
                Open = d.Open,
                High = d.High,
                Low = d.Low,
                Close = d.Close,
                Volume = d.Volume
            });
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
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

