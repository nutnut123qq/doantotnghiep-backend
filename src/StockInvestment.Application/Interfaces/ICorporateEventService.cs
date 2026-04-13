using StockInvestment.Application.Contracts.AI;

namespace StockInvestment.Application.Interfaces;

public interface ICorporateEventService
{
    Task<QuestionAnswerResult> AskQuestionAsync(
        string symbol,
        string question,
        int days = 90,
        int topK = 6,
        CancellationToken cancellationToken = default);
}
