namespace StockInvestment.Domain.Entities;

public class AIModelConfig
{
    public Guid Id { get; set; }
    public string ModelName { get; set; } = string.Empty; // "Gemini", "GPT-4", etc.
    public string Version { get; set; } = string.Empty; // "gemini-pro-1.5"
    public string? ApiKey { get; set; } // Encrypted
    public string? Settings { get; set; } // JSON string for additional settings
    public int UpdateFrequencyMinutes { get; set; } = 60; // Forecast update frequency
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

