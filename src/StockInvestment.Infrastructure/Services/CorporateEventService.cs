using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class CorporateEventService : ICorporateEventService
{
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

        foreach (var ev in candidates)
        {
            await CorporateEventRagHelper.TryIngestForRagAsync(
                _aiService,
                ev,
                normalizedSymbol,
                _logger,
                cancellationToken);
        }

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
}
