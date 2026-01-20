using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Services;

public class NewsService : INewsService
{
    private readonly ILogger<NewsService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;

    public NewsService(
        ILogger<NewsService> logger,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<IEnumerable<News>> GetNewsAsync(int page = 1, int pageSize = 20, Guid? tickerId = null)
    {
        var query = _context.News.AsQueryable();

        if (tickerId.HasValue)
        {
            query = query.Where(n => n.TickerId == tickerId.Value);
        }

        var news = await query
            .OrderByDescending(n => n.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return news;
    }

    public async Task<News?> GetNewsByIdAsync(Guid id)
    {
        return await _unitOfWork.Repository<News>().GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetRecentNewsForSymbolAsync(string symbol, int days = 7, int limit = 5)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        var ticker = await _context.StockTickers
            .FirstOrDefaultAsync(t => t.Symbol == normalizedSymbol);

        if (ticker == null)
        {
            _logger.LogWarning("No ticker found for symbol {Symbol} when querying news", normalizedSymbol);
            return Array.Empty<NewsItemDto>();
        }

        var sinceDate = DateTime.UtcNow.AddDays(-days);
        var newsList = await _context.News
            .Where(n => n.TickerId == ticker.Id && n.PublishedAt >= sinceDate)
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .ToListAsync();

        return newsList.Select(n => new NewsItemDto
        {
            Id = n.Id,
            Title = n.Title,
            PublishedAt = n.PublishedAt,
            Url = n.Url,
            Summary = n.Summary
        }).ToList();
    }

    public Task RequestSummarizationAsync(Guid newsId)
    {
        // This is handled by the controller via message queue
        return Task.CompletedTask;
    }

    public async Task<News> AddNewsAsync(News news)
    {
        try
        {
            await _unitOfWork.Repository<News>().AddAsync(news);
            await _unitOfWork.SaveChangesAsync();
            return news;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding news to database");
            throw;
        }
    }

    public async Task<IEnumerable<News>> AddNewsRangeAsync(IEnumerable<News> newsList)
    {
        try
        {
            await _unitOfWork.Repository<News>().AddRangeAsync(newsList);
            await _unitOfWork.SaveChangesAsync();
            return newsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding news range to database");
            throw;
        }
    }

    public async Task UpdateNewsAsync(News news)
    {
        try
        {
            await _unitOfWork.Repository<News>().UpdateAsync(news);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating news {Id}", news.Id);
            throw;
        }
    }
}

