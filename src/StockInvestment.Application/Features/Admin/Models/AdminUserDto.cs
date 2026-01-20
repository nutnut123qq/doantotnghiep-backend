using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Admin.Models;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static AdminUserDto FromEntity(User user)
    {
        return new AdminUserDto
        {
            Id = user.Id,
            Email = user.Email.Value,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
