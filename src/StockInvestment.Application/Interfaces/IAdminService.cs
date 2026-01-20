using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Application.Features.Admin.Models;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Service interface for admin operations
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Get all users with pagination
    /// </summary>
    Task<(IEnumerable<User> Users, int TotalCount)> GetAllUsersAsync(int page = 1, int pageSize = 20);
    
    /// <summary>
    /// Get user by ID
    /// </summary>
    Task<User?> GetUserByIdAsync(Guid userId);
    
    /// <summary>
    /// Update user role
    /// </summary>
    Task<bool> UpdateUserRoleAsync(Guid userId, Domain.Enums.UserRole newRole);
    
    /// <summary>
    /// Activate/deactivate user account
    /// </summary>
    Task<bool> SetUserActiveStatusAsync(Guid userId, bool isActive);
    
    /// <summary>
    /// Delete user account
    /// </summary>
    Task<bool> DeleteUserAsync(Guid userId);
    
    /// <summary>
    /// Get system statistics
    /// </summary>
    Task<SystemStats> GetSystemStatsAsync();

    /// <summary>
    /// Create user (admin)
    /// </summary>
    Task<AdminActionResult<AdminUserDto>> CreateUserAsync(Guid adminUserId, string email, string password, UserRole role, string? fullName);

    /// <summary>
    /// Update user info/role (admin)
    /// </summary>
    Task<AdminActionResult> UpdateUserAsync(Guid adminUserId, Guid userId, string? email, string? fullName, UserRole? role);

    /// <summary>
    /// Reset user password (admin)
    /// </summary>
    Task<AdminActionResult> ResetPasswordAsync(Guid adminUserId, Guid userId, string newPassword);

    /// <summary>
    /// Lock user account (admin)
    /// </summary>
    Task<AdminActionResult> LockUserAsync(Guid adminUserId, Guid userId);

    /// <summary>
    /// Unlock user account (admin)
    /// </summary>
    Task<AdminActionResult> UnlockUserAsync(Guid adminUserId, Guid userId);
}

/// <summary>
/// System statistics DTO
/// </summary>
public class SystemStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int AdminUsers { get; set; }
    public int TotalStocks { get; set; }
    public int TotalWatchlists { get; set; }
    public int TotalAlerts { get; set; }
    public int TotalEvents { get; set; }
    public int TotalNews { get; set; }
    public DateTime LastUpdated { get; set; }
}
