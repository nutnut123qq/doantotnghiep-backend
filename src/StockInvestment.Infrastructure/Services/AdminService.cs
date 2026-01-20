using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Features.Admin.Models;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Service for admin operations
/// </summary>
public class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public AdminService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetAllUsersAsync(int page = 1, int pageSize = 20)
    {
        var users = await _unitOfWork.Users.GetAllAsync();
        var totalCount = users.Count();
        
        var pagedUsers = users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (pagedUsers, totalCount);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _unitOfWork.Users.GetByIdAsync(userId);
    }

    public async Task<bool> UpdateUserRoleAsync(Guid userId, UserRole newRole)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return false;

        user.Role = newRole;
        user.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.Repository<User>().UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> SetUserActiveStatusAsync(Guid userId, bool isActive)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return false;

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.Repository<User>().UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return false;

        // Soft delete by setting IsActive = false
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        
        await _unitOfWork.Repository<User>().UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    public async Task<SystemStats> GetSystemStatsAsync()
    {
        var users = await _unitOfWork.Users.GetAllAsync();
        var stocks = await _unitOfWork.Repository<StockTicker>().GetAllAsync();
        var watchlists = await _unitOfWork.Watchlists.GetAllAsync();
        var alerts = await _unitOfWork.Alerts.GetAllAsync();
        var events = await _unitOfWork.CorporateEvents.GetAllAsync();
        var news = await _unitOfWork.Repository<News>().GetAllAsync();

        return new SystemStats
        {
            TotalUsers = users.Count(),
            ActiveUsers = users.Count(u => u.IsActive),
            AdminUsers = users.Count(u => u.Role == UserRole.Admin),
            TotalStocks = stocks.Count(),
            TotalWatchlists = watchlists.Count(),
            TotalAlerts = alerts.Count(),
            TotalEvents = events.Count(),
            TotalNews = news.Count(),
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<AdminActionResult<AdminUserDto>> CreateUserAsync(Guid adminUserId, string email, string password, UserRole role, string? fullName)
    {
        var payload = new { email, role, fullName };
        try
        {
            var emailVo = Domain.ValueObjects.Email.Create(email);
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(emailVo);
            if (existingUser != null)
            {
                await LogAdminActionAsync(adminUserId, existingUser.Id, "CreateUser", payload, false, "Email already exists");
                return new AdminActionResult<AdminUserDto> { Success = false, ErrorMessage = "Email already exists" };
            }

            var user = new User
            {
                Email = emailVo,
                PasswordHash = _passwordHasher.HashPassword(password),
                Role = role,
                FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim(),
                IsEmailVerified = false,
                IsActive = true,
                LockoutEnabled = false,
                LockoutEnd = null
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await LogAdminActionAsync(adminUserId, user.Id, "CreateUser", payload, true, null);
            return new AdminActionResult<AdminUserDto> { Success = true, Data = AdminUserDto.FromEntity(user) };
        }
        catch (Exception ex)
        {
            await LogAdminActionAsync(adminUserId, null, "CreateUser", payload, false, ex.Message);
            return new AdminActionResult<AdminUserDto> { Success = false, ErrorMessage = "Failed to create user" };
        }
    }

    public async Task<AdminActionResult> UpdateUserAsync(Guid adminUserId, Guid userId, string? email, string? fullName, UserRole? role)
    {
        var payload = new { email, fullName, role };
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
            {
                await LogAdminActionAsync(adminUserId, userId, "UpdateUser", payload, false, "User not found");
                return new AdminActionResult { Success = false, ErrorMessage = "User not found" };
            }

            if (!string.IsNullOrWhiteSpace(email) && !string.Equals(user.Email.Value, email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var emailVo = Domain.ValueObjects.Email.Create(email.Trim());
                var existing = await _unitOfWork.Users.GetByEmailAsync(emailVo);
                if (existing != null && existing.Id != userId)
                {
                    await LogAdminActionAsync(adminUserId, userId, "UpdateUser", payload, false, "Email already exists");
                    return new AdminActionResult { Success = false, ErrorMessage = "Email already exists" };
                }
                user.Email = emailVo;
            }

            if (fullName != null)
            {
                user.FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
            }

            if (role.HasValue)
            {
                user.Role = role.Value;
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await LogAdminActionAsync(adminUserId, userId, "UpdateUser", payload, true, null);
            return new AdminActionResult { Success = true };
        }
        catch (Exception ex)
        {
            await LogAdminActionAsync(adminUserId, userId, "UpdateUser", payload, false, ex.Message);
            return new AdminActionResult { Success = false, ErrorMessage = "Failed to update user" };
        }
    }

    public async Task<AdminActionResult> ResetPasswordAsync(Guid adminUserId, Guid userId, string newPassword)
    {
        var payload = new { userId };
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
            {
                await LogAdminActionAsync(adminUserId, userId, "ResetPassword", payload, false, "User not found");
                return new AdminActionResult { Success = false, ErrorMessage = "User not found" };
            }

            var token = new PasswordResetToken
            {
                UserId = userId,
                Token = Guid.NewGuid().ToString("N")
            };

            await _unitOfWork.Repository<PasswordResetToken>().AddAsync(token);
            await _unitOfWork.SaveChangesAsync();

            if (!token.IsValid)
            {
                await LogAdminActionAsync(adminUserId, userId, "ResetPassword", payload, false, "Reset token invalid");
                return new AdminActionResult { Success = false, ErrorMessage = "Reset token invalid" };
            }

            user.PasswordHash = _passwordHasher.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            token.IsUsed = true;
            token.UsedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.Repository<PasswordResetToken>().UpdateAsync(token);
            await _unitOfWork.SaveChangesAsync();

            await LogAdminActionAsync(adminUserId, userId, "ResetPassword", payload, true, null);
            return new AdminActionResult { Success = true };
        }
        catch (Exception ex)
        {
            await LogAdminActionAsync(adminUserId, userId, "ResetPassword", payload, false, ex.Message);
            return new AdminActionResult { Success = false, ErrorMessage = "Failed to reset password" };
        }
    }

    public async Task<AdminActionResult> LockUserAsync(Guid adminUserId, Guid userId)
    {
        var payload = new { userId };
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
            {
                await LogAdminActionAsync(adminUserId, userId, "LockUser", payload, false, "User not found");
                return new AdminActionResult { Success = false, ErrorMessage = "User not found" };
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await LogAdminActionAsync(adminUserId, userId, "LockUser", payload, true, null);
            return new AdminActionResult { Success = true };
        }
        catch (Exception ex)
        {
            await LogAdminActionAsync(adminUserId, userId, "LockUser", payload, false, ex.Message);
            return new AdminActionResult { Success = false, ErrorMessage = "Failed to lock user" };
        }
    }

    public async Task<AdminActionResult> UnlockUserAsync(Guid adminUserId, Guid userId)
    {
        var payload = new { userId };
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
            {
                await LogAdminActionAsync(adminUserId, userId, "UnlockUser", payload, false, "User not found");
                return new AdminActionResult { Success = false, ErrorMessage = "User not found" };
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await LogAdminActionAsync(adminUserId, userId, "UnlockUser", payload, true, null);
            return new AdminActionResult { Success = true };
        }
        catch (Exception ex)
        {
            await LogAdminActionAsync(adminUserId, userId, "UnlockUser", payload, false, ex.Message);
            return new AdminActionResult { Success = false, ErrorMessage = "Failed to unlock user" };
        }
    }

    private async Task LogAdminActionAsync(Guid adminUserId, Guid? targetUserId, string action, object? payload, bool isSuccess, string? errorMessage)
    {
        var auditLog = new AdminAuditLog
        {
            AdminUserId = adminUserId,
            TargetUserId = targetUserId,
            Action = action,
            Payload = payload == null ? null : JsonSerializer.Serialize(payload),
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage
        };

        await _unitOfWork.Repository<AdminAuditLog>().AddAsync(auditLog);
        await _unitOfWork.SaveChangesAsync();
    }
}
