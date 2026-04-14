using StockInvestment.Application.Contracts.AI;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface ICorporateEventService
{
    Task<(IReadOnlyList<CorporateEvent> Items, int TotalCount)> GetEventsForAdminAsync(
        int page = 1,
        int pageSize = 20,
        string? symbol = null,
        CorporateEventType? eventType = null,
        EventStatus? status = null);
    Task<bool> SetEventDeletedAsync(Guid id, bool isDeleted);
    Task<QuestionAnswerResult> AskQuestionAsync(
        string symbol,
        string question,
        int days = 90,
        int topK = 6,
        CancellationToken cancellationToken = default);
}
