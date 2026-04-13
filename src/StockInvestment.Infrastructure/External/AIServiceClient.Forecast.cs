using System.Net.Http.Json;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.External;

public partial class AIServiceClient
{
    [Obsolete("Use GenerateForecastBySymbolAsync instead. Guid-based forecast contract is legacy.")]
    public async Task<object> GenerateForecastAsync(Guid tickerId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/forecast", new { tickerId }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>(cancellationToken: cancellationToken) ?? new { };
    }

    public async Task<ForecastResult> GenerateForecastBySymbolAsync(string symbol, string timeHorizon = "short", CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var endpoint = $"/api/forecast/{symbol}";
        var response = await _httpClient.GetAsync($"{endpoint}?time_horizon={timeHorizon}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleHttpErrorAsync(response, endpoint, symbol);
        }

        var result = await response.Content.ReadFromJsonAsync<ForecastApiResponse>(cancellationToken: cancellationToken);
        return ParseForecastResponse(result);
    }

    public async Task<ForecastResult> GenerateForecastWithDataAsync(string symbol, string timeHorizon, Dictionary<string, string>? technicalData, Dictionary<string, string>? fundamentalData, Dictionary<string, string>? sentimentData, CancellationToken cancellationToken = default)
    {
        EnsureBaseAddressConfigured();
        var request = new
        {
            symbol,
            technical_data = technicalData ?? new Dictionary<string, string>(),
            fundamental_data = fundamentalData ?? new Dictionary<string, string>(),
            sentiment_data = sentimentData ?? new Dictionary<string, string>(),
            time_horizon = timeHorizon
        };

        var endpoint = "/api/forecast/generate";
        var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleHttpErrorAsync(response, endpoint, symbol);
        }

        var result = await response.Content.ReadFromJsonAsync<ForecastApiResponse>(cancellationToken: cancellationToken);
        return ParseForecastResponse(result);
    }
}
