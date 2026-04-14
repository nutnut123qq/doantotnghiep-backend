using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class CorporateEventService : ICorporateEventService
{
    private const int MaxEventsToIngestPerQuestion = 10;
    private static readonly TimeSpan IngestBudget = TimeSpan.FromSeconds(20);

    private readonly ILogger<CorporateEventService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIService _aiService;

    public CorporateEventService(
        ILogger<CorporateEventService> logger,
        IUnitOfWork unitOfWork,
        IAIService aiService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _aiService = aiService;
    }

    public Task<(IReadOnlyList<CorporateEvent> Items, int TotalCount)> GetEventsForAdminAsync(
        int page = 1,
        int pageSize = 20,
        string? symbol = null,
        CorporateEventType? eventType = null,
        EventStatus? status = null)
        => _unitOfWork.CorporateEvents.GetForAdminAsync(page, pageSize, symbol, eventType, status);

    public Task<bool> SetEventDeletedAsync(Guid id, bool isDeleted)
        => _unitOfWork.CorporateEvents.SetDeletedAsync(id, isDeleted);

    public async Task<QuestionAnswerResult> AskQuestionAsync(
        string symbol,
        string question,
        int days = 90,
        int topK = 6,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        var since = DateTime.UtcNow.AddDays(-Math.Abs(days));
        var limit = Math.Clamp(topK * 3, 8, 40);

        var candidates = await _unitOfWork.CorporateEvents.GetRecentBySymbolAsync(normalizedSymbol, since, limit);

        if (candidates.Count == 0)
        {
            return new QuestionAnswerResult
            {
                Answer = $"Không có dữ liệu sự kiện doanh nghiệp gần đây cho mã {normalizedSymbol}."
            };
        }

        await IngestRecentEventsForRagAsync(candidates, normalizedSymbol, cancellationToken);

        static string Cap(string? text, int maxLen)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Length <= maxLen ? text : text[..maxLen] + "…";
        }

        var baseContext = string.Join(
            "\n\n",
            candidates.Select(e =>
                $"{e.EventDate:yyyy-MM-dd} | {e.EventType} | {e.Title}\n{Cap(e.Description, 800)}"));

        return await _aiService.AnswerQuestionAsync(
            question: question,
            baseContext: baseContext,
            source: CorporateEventRagHelper.RagSource,
            symbol: normalizedSymbol,
            topK: topK,
            cancellationToken: cancellationToken);
    }

    private async Task IngestRecentEventsForRagAsync(
        IReadOnlyList<CorporateEvent> candidates,
        string symbol,
        CancellationToken cancellationToken)
    {
        // Keep request latency predictable by ingesting only a small, freshest subset.
        var eventsToIngest = candidates
            .OrderByDescending(e => e.EventDate)
            .Take(MaxEventsToIngestPerQuestion)
            .ToList();

        if (eventsToIngest.Count == 0)
            return;

        using var ingestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ingestCts.CancelAfter(IngestBudget);

        try
        {
            var ingestTasks = eventsToIngest.Select(ev =>
                CorporateEventRagHelper.TryIngestForRagAsync(
                    _aiService,
                    ev,
                    symbol,
                    _logger,
                    ingestCts.Token));

            await Task.WhenAll(ingestTasks);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Corporate event RAG ingest exceeded {IngestBudgetMs}ms budget for {Symbol}; continuing with available context.",
                IngestBudget.TotalMilliseconds,
                symbol);
        }
    }
}
