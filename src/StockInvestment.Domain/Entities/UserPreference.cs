using System;

namespace StockInvestment.Domain.Entities;

/// <summary>
/// Stores user-specific preferences and settings
/// </summary>
public class UserPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    /// <summary>
    /// Preference key (e.g., "dashboard_layout", "theme", "notifications")
    /// </summary>
    public string PreferenceKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Preference value stored as JSON string
    /// </summary>
    public string PreferenceValue { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
