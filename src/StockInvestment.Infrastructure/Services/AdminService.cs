using Microsoft.EntityFrameworkCore;
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

    public AdminService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
}
