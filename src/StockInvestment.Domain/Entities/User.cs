using StockInvestment.Domain.Enums;
using StockInvestment.Domain.ValueObjects;

namespace StockInvestment.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Email Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<Layout> Layouts { get; set; } = new List<Layout>();

    public User()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Role = UserRole.Investor;
        IsEmailVerified = false;
        IsActive = true;
    }
}

