using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Contracts.AI;
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
    private readonly IAIService _aiService;

    public NewsService(
        ILogger<NewsService> logger,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        IAIService aiService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _context = context;
        _aiService = aiService;
    }

    public async Task<IEnumerable<News>> GetNewsAsync(int page = 1, int pageSize = 20, Guid? tickerId = null)
    {
        var query = _context.News.Where(n => !n.IsDeleted);

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

    public async Task<(IReadOnlyList<News> Items, int TotalCount)> GetNewsForAdminAsync(int page = 1, int pageSize = 20, Guid? tickerId = null)
    {
        var query = _context.News.AsQueryable();

        if (tickerId.HasValue)
        {
            query = query.Where(n => n.TickerId == tickerId.Value);
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.PublishedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public async Task<News?> GetNewsByIdAsync(Guid id)
    {
        var news = await _unitOfWork.Repository<News>().GetByIdAsync(id);
        if (news == null || news.IsDeleted)
        {
            return null;
        }

        return news;
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
        var pattern = $"%{normalizedSymbol}%";
        var newsList = await _context.News
            .Where(n => !n.IsDeleted)
            .Where(n => n.PublishedAt >= sinceDate)
            .Where(n =>
                n.TickerId == ticker.Id
                || (n.TickerId == null
                    && (EF.Functions.ILike(n.Title, pattern)
                        || EF.Functions.ILike(n.Content, pattern)
                        || (n.Summary != null && EF.Functions.ILike(n.Summary, pattern)))))
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .ToListAsync();

        return newsList.Select(n => new NewsItemDto
        {
            Id = n.Id,
            Title = n.Title,
            PublishedAt = n.PublishedAt,
            Url = n.Url,
            Summary = n.Summary ?? n.Content
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

    public async Task<bool> SetNewsDeletedAsync(Guid id, bool isDeleted)
    {
        var news = await _unitOfWork.Repository<News>().GetByIdAsync(id);
        if (news == null)
        {
            return false;
        }

        if (news.IsDeleted == isDeleted)
        {
            return true;
        }

        news.IsDeleted = isDeleted;
        await UpdateNewsAsync(news);
        return true;
    }

    public async Task<HashSet<string>> GetExistingUrlsAsync()
    {
        var urls = await _context.News
            .Where(n => n.Url != null)
            .Select(n => n.Url!)
            .Distinct()
            .ToListAsync();

        return new HashSet<string>(urls, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<HashSet<string>> GetExistingFingerprintsAsync()
    {
        var records = await _context.News
            .Select(n => new { n.Title, n.Source, n.PublishedAt })
            .ToListAsync();

        var fingerprints = records
            .Select(r => BuildFingerprint(r.Title, r.Source, r.PublishedAt))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return fingerprints;
    }

    public async Task<QuestionAnswerResult> AskQuestionAsync(string? symbol, string question, int days = 7, int topK = 6)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Abs(days));
        var limit = Math.Clamp(topK * 3, 8, 30);
        var normalizedSymbol = string.IsNullOrWhiteSpace(symbol)
            ? null
            : symbol.Trim().ToUpperInvariant();

        IQueryable<News> query = _context.News
            .Include(n => n.Ticker)
            .Where(n => !n.IsDeleted)
            .Where(n => n.PublishedAt >= since);

        if (normalizedSymbol != null)
        {
            var pattern = $"%{normalizedSymbol}%";
            query = query.Where(n =>
                (n.Ticker != null && n.Ticker.Symbol == normalizedSymbol)
                || EF.Functions.ILike(n.Title, pattern)
                || EF.Functions.ILike(n.Content, pattern)
                || (n.Summary != null && EF.Functions.ILike(n.Summary, pattern)));
        }

        var candidates = await query
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .ToListAsync();

        if (!candidates.Any())
        {
            return new QuestionAnswerResult
            {
                Answer = normalizedSymbol != null
                    ? $"Không có dữ liệu tin tức gần đây cho mã {normalizedSymbol}."
                    : "Không có dữ liệu tin tức gần đây."
            };
        }

        // Full text is sent in baseContext; AI service skips vector ingest/search for news (see QAService).
        // Avoiding per-article IngestDocumentAsync saves many sequential HTTP calls to the AI service.
        var baseContext = string.Join(
            "\n\n",
            candidates.Select(n => $"{n.PublishedAt:yyyy-MM-dd} | {n.Source} | {n.Title}\n{Cap(n.Summary ?? n.Content, 800)}"));

        var result = await _aiService.AnswerQuestionAsync(
            question: question,
            baseContext: baseContext,
            source: "news",
            symbol: normalizedSymbol,
            topK: topK);

        if (result.Sources.Count == 0)
        {
            result.Sources = candidates
                .Take(topK)
                .Select(n => new SourceObject
                {
                    DocumentId = n.Id.ToString(),
                    Source = "news",
                    SourceUrl = n.Url,
                    Title = n.Title,
                    Section = n.Source ?? string.Empty,
                    Symbol = normalizedSymbol ?? n.Ticker?.Symbol ?? string.Empty,
                    TextPreview = Cap(n.Summary ?? n.Content, 350),
                })
                .ToList();
        }

        return result;
    }

    public static string BuildFingerprint(string? title, string? source, DateTime publishedAt)
    {
        var normalizedTitle = NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return string.Empty;
        }

        return $"{normalizedTitle}|{(source ?? string.Empty).Trim().ToLowerInvariant()}|{publishedAt:yyyy-MM-dd}";
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var lower = title.Trim().ToLowerInvariant();
        var compact = System.Text.RegularExpressions.Regex.Replace(lower, @"\s+", " ");
        return compact;
    }

    private static string Cap(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

