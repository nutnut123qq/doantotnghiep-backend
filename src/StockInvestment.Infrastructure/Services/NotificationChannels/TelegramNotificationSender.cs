using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Configuration;

namespace StockInvestment.Infrastructure.Services.NotificationChannels;

public class TelegramNotificationSender : INotificationChannelSender
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<NotificationChannelOptions> _options;
    private readonly ILogger<TelegramNotificationSender> _logger;

    public NotificationChannelType ChannelType => NotificationChannelType.Telegram;

    public TelegramNotificationSender(
        HttpClient httpClient,
        IOptions<NotificationChannelOptions> options,
        ILogger<TelegramNotificationSender> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> SendAsync(NotificationSendRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var botToken = _options.Value.Telegram.BotToken;
            if (string.IsNullOrEmpty(botToken))
            {
                _logger.LogWarning("Telegram bot token not configured");
                return false;
            }

            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new
            {
                chat_id = request.Destination,
                text = EscapeMarkdownV2(request.Message),
                parse_mode = "MarkdownV2"
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            
            // Đọc body as string TRƯỚC để handle non-JSON (proxy/WAF errors)
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
            TelegramResponse? result = null;
            
            try
            {
                result = JsonSerializer.Deserialize<TelegramResponse>(rawBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException)
            {
                // Body không phải JSON (proxy/WAF/HTML error)
                _logger.LogWarning("Telegram API HTTP {StatusCode}, non-JSON response: {Body}", 
                    response.StatusCode, 
                    rawBody.Length > 200 ? rawBody.Substring(0, 200) + "..." : rawBody);  // Truncate
                return false;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                // Log HTTP error + parsed error details (không chứa token)
                _logger.LogWarning("Telegram API HTTP {StatusCode}, Error: {ErrorCode} - {Description}", 
                    response.StatusCode, result?.ErrorCode, result?.Description);
                return false;
            }

            if (result?.Ok == true)
                return true;
            
            // Log API-level error (ok=false trong 200 response)
            _logger.LogWarning("Telegram API error: {ErrorCode} - {Description}", 
                result?.ErrorCode, result?.Description);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification");  // No token in log
            return false;
        }
    }

    private static string EscapeMarkdownV2(string text)
    {
        // Escape backslash FIRST to avoid double-escape
        text = text.Replace("\\", "\\\\");
        
        // Then escape other MarkdownV2 special chars
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        foreach (var c in specialChars)
            text = text.Replace(c.ToString(), $"\\{c}");
        
        return text;
    }

    private class TelegramResponse
    {
        public bool Ok { get; set; }
        
        [JsonPropertyName("error_code")]
        public int? ErrorCode { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
