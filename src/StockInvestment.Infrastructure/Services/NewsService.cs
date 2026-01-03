using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news from database");
            return Enumerable.Empty<News>();
        }
    }

    public async Task<News?> GetNewsByIdAsync(Guid id)
    {
        try
        {
            return await _unitOfWork.Repository<News>().GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news {Id} from database", id);
            return null;
        }
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

