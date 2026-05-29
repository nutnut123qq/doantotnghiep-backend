using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Contracts.AI;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.Common;
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

    public async Task<(IReadOnlyList<News> Items, int TotalCount)> GetNewsAsync(int page = 1, int pageSize = 20, Guid? tickerId = null)
    {
        var query = _context.News.Where(n => !n.IsDeleted);

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
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var ticker = await _context.StockTickers
            .FirstOrDefaultAsync(t => t.Symbol == normalizedSymbol);

        if (ticker == null)
        {
            _logger.LogWarning("No ticker found for symbol {Symbol} when querying news", normalizedSymbol);
            return Array.Empty<NewsItemDto>();
        }

        var sinceDate = DateTime.UtcNow.AddDays(-days);
        var searchPhrases = NewsTickerResolver.GetSearchPhrasesForTicker(ticker);

        var candidates = await _context.News
            .Where(n => !n.IsDeleted)
            .Where(n => n.PublishedAt >= sinceDate)
            .Where(n => n.TickerId == ticker.Id || n.TickerId == null)
            .OrderByDescending(n => n.PublishedAt)
            .Take(Math.Clamp(limit * 8, limit, 80))
            .ToListAsync();

        var newsList = candidates
            .Where(n => MatchesSymbolNews(n, ticker.Id, normalizedSymbol, searchPhrases))
            .Take(limit)
            .ToList();

        return newsList.Select(n => new NewsItemDto
        {
            Id = n.Id,
            Title = n.Title,
            PublishedAt = n.PublishedAt,
            Url = n.Url,
            Summary = n.Summary ?? n.Content
        }).ToList();
    }

    public async Task<int> BackfillTickerIdsAsync(int batchSize = 500, CancellationToken cancellationToken = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 5000);

        var tickers = await _context.StockTickers.AsNoTracking().ToListAsync(cancellationToken);
        if (tickers.Count == 0)
            return 0;

        var tickerMap = NewsTickerResolver.BuildTickerMap(tickers);
        var aliasMap = NewsTickerResolver.BuildTickerNameAliasMap(tickers);

        var batch = await _context.News
            .Where(n => !n.IsDeleted && n.TickerId == null)
            .OrderByDescending(n => n.PublishedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var news in batch)
        {
            if (!NewsTickerResolver.TryResolveTickerId(news, tickerMap, aliasMap, out var tickerId))
                continue;

            news.TickerId = tickerId;
            updated++;
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Backfilled TickerId on {Count} news rows", updated);
        }

        return updated;
    }

    private static bool MatchesSymbolNews(
        News news,
        Guid tickerId,
        string normalizedSymbol,
        IReadOnlyList<string> searchPhrases)
    {
        if (news.TickerId == tickerId)
            return true;

        if (news.TickerId.HasValue)
            return false;

        var combined = NewsTickerResolver.CombineNewsText(news);
        if (string.IsNullOrWhiteSpace(combined))
            return false;

        var upper = combined.ToUpperInvariant();
        if (ContainsSymbolToken(upper, normalizedSymbol))
            return true;

        var normalizedCombined = NormalizeForSearch(combined);
        foreach (var phrase in searchPhrases)
        {
            if (phrase.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase))
                continue;

            var normalizedPhrase = NormalizeForSearch(phrase);
            if (normalizedPhrase.Length >= 6
                && normalizedCombined.Contains(normalizedPhrase, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsSymbolToken(string upperCombined, string symbol)
    {
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                     upperCombined,
                     @"\b([A-Z]{3,5})\b"))
        {
            if (m.Groups[1].Value.Equals(symbol, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string NormalizeForSearch(string text)
    {
        var lower = text.ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        foreach (var ch in lower.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return System.Text.RegularExpressions.Regex
            .Replace(sb.ToString().Normalize(System.Text.NormalizationForm.FormC), @"[\p{P}\p{S}\s]+", " ")
            .Trim();
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

