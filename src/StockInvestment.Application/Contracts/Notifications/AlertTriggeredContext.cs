using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Contracts.Notifications;

public class AlertTriggeredContext
{
    public Alert Alert { get; set; } = null!;
    public Guid UserId { get; set; }
    public decimal CurrentValue { get; set; }  // Runtime price/volume từ check condition
    public DateTime TriggeredAt { get; set; }
    
    // Store operator directly thay vì parse từ MatchedCondition
    public string Operator { get; set; } = null!;  // ">", "<", ">=", "<=", "="
    public string MatchedCondition { get; set; } = null!;  // "Price > 100,000" (for logging/display)
    
    public string AiExplanation { get; set; } = "AI explanation unavailable";  // Non-nullable with default fallback
}
