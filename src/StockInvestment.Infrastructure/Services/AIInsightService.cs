using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Domain.Constants;

namespace StockInvestment.Infrastructure.Services;

public class AIInsightService : IAIInsightService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIInsightRepository _aiInsightRepository;
    private readonly IStockTickerRepository _stockTickerRepository;
    private readonly IAIService _aiService;
    private readonly ITechnicalDataService _technicalDataService;
    private readonly INewsService _newsService;
    private readonly ILogger<AIInsightService> _logger;

    public AIInsightService(
        IUnitOfWork unitOfWork,
        IAIInsightRepository aiInsightRepository,
        IStockTickerRepository stockTickerRepository,
        IAIService aiService,
        ITechnicalDataService technicalDataService,
        INewsService newsService,
        ILogger<AIInsightService> logger)
    {
        _unitOfWork = unitOfWork;
        _aiInsightRepository = aiInsightRepository;
        _stockTickerRepository = stockTickerRepository;
        _aiService = aiService;
        _technicalDataService = technicalDataService;
        _newsService = newsService;
        _logger = logger;
    }

    public async Task<IEnumerable<AIInsight>> GetInsightsAsync(
        InsightType? type = null,
        string? symbol = null,
        bool includeDismissed = false,
        CancellationToken cancellationToken = default)
    {
        return await _aiInsightRepository.GetInsightsAsync(type, symbol, includeDismissed, cancellationToken);
    }

    public async Task<AIInsight?> GetInsightByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _aiInsightRepository.GetInsightByIdWithTickerAsync(id, cancellationToken);
    }

    public async Task<AIInsight> GenerateInsightAsync(
        Guid tickerId,
        Dictionary<string, string>? technicalData = null,
        Dictionary<string, string>? fundamentalData = null,
        Dictionary<string, string>? sentimentData = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ticker = await _stockTickerRepository.GetByIdAsync(tickerId, cancellationToken);
            if (ticker == null)
            {
                throw new Domain.Exceptions.NotFoundException("Ticker", tickerId);
            }

            // Collect technical indicators if not provided
            if (technicalData == null)
            {
                technicalData = await _technicalDataService.PrepareTechnicalDataAsync(ticker.Symbol, cancellationToken);
                technicalData["volume"] = ticker.Volume?.ToString() ?? "N/A";
            }

            // Collect news sentiment if not provided
            if (sentimentData == null)
            {
                var recentNews = await _newsService.GetNewsAsync(1, 10, tickerId);
                var newsList = recentNews.ToList();
                
                sentimentData = new Dictionary<string, string>();
                if (newsList.Any())
                {
                    var positiveCount = newsList.Count(n => n.Sentiment == Sentiment.Positive);
                    var negativeCount = newsList.Count(n => n.Sentiment == Sentiment.Negative);
                    var totalCount = newsList.Count;
                    
                    var sentimentScore = totalCount > 0 
                        ? (double)(positiveCount - negativeCount) / totalCount 
                        : 0.0;
                    
                    sentimentData["score"] = sentimentScore.ToString("F2");
                    sentimentData["sentiment"] = sentimentScore > TechnicalIndicatorConstants.SENTIMENT_POSITIVE_THRESHOLD 
                        ? "Tích cực" 
                        : sentimentScore < TechnicalIndicatorConstants.SENTIMENT_NEGATIVE_THRESHOLD 
                            ? "Tiêu cực" 
                            : "Trung lập";
                    sentimentData["recent_news"] = $"Có {totalCount} tin tức gần đây";
                }
                else
                {
                    sentimentData["score"] = "0";
                    sentimentData["sentiment"] = "Trung lập";
                    sentimentData["recent_news"] = "Chưa có tin tức";
                }
            }

            // Call AI service to generate insight
            _logger.LogInformation("Calling AI service to generate insight for {Symbol}", ticker.Symbol);
            InsightResult aiResult;
            try
            {
                aiResult = await _aiService.GenerateInsightAsync(
                    ticker.Symbol,
                    technicalData,
                    fundamentalData,
                    sentimentData);
                _logger.LogInformation("Successfully received insight from AI service for {Symbol}: Type={Type}, Confidence={Confidence}", 
                    ticker.Symbol, aiResult.Type, aiResult.Confidence);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error calling AI service for {Symbol}", ticker.Symbol);
                throw new Domain.Exceptions.ExternalServiceException(
                    "AI Service",
                    $"Không thể kết nối đến AI service: {httpEx.Message}. Vui lòng kiểm tra AI service đang chạy tại http://localhost:8000",
                    httpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AI service for {Symbol}", ticker.Symbol);
                throw new Domain.Exceptions.ExternalServiceException(
                    "AI Service",
                    $"Lỗi khi gọi AI service: {ex.Message}",
                    ex);
            }

            // Map InsightType from string
            InsightType insightType = InsightType.Hold;
            if (aiResult.Type.Equals("Buy", StringComparison.OrdinalIgnoreCase))
                insightType = InsightType.Buy;
            else if (aiResult.Type.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                insightType = InsightType.Sell;

            // Check if insight already exists for this ticker and type
            var existingInsight = await _aiInsightRepository.FindActiveInsightAsync(tickerId, insightType, cancellationToken);

            // Serialize reasoning to JSON
            var reasoningJson = JsonSerializer.Serialize(aiResult.Reasoning);

            if (existingInsight != null)
            {
                // Update existing insight
                existingInsight.Title = aiResult.Title;
                existingInsight.Description = aiResult.Description;
                existingInsight.Confidence = aiResult.Confidence;
                existingInsight.Reasoning = reasoningJson;
                existingInsight.TargetPrice = aiResult.TargetPrice;
                existingInsight.StopLoss = aiResult.StopLoss;
                existingInsight.GeneratedAt = aiResult.GeneratedAt;
                existingInsight.UpdatedAt = DateTime.UtcNow;

                await _aiInsightRepository.UpdateAsync(existingInsight);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                // Reload with Ticker navigation property
                var updatedInsight = await _aiInsightRepository.GetInsightByIdWithTickerAsync(existingInsight.Id, cancellationToken);
                
                _logger.LogInformation("Updated AI insight {Id} for ticker {TickerId}", existingInsight.Id, tickerId);
                return updatedInsight ?? existingInsight;
            }
            else
            {
                // Create new insight
                var insight = new AIInsight
                {
                    TickerId = tickerId,
                    Type = insightType,
                    Title = aiResult.Title,
                    Description = aiResult.Description,
                    Confidence = aiResult.Confidence,
                    Reasoning = reasoningJson,
                    TargetPrice = aiResult.TargetPrice,
                    StopLoss = aiResult.StopLoss,
                    GeneratedAt = aiResult.GeneratedAt,
                    Ticker = ticker // Set navigation property
                };

                await _aiInsightRepository.AddAsync(insight, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                // Reload with Ticker navigation property
                var savedInsight = await _aiInsightRepository.GetInsightByIdWithTickerAsync(insight.Id, cancellationToken);
                
                _logger.LogInformation("Created AI insight {Id} for ticker {TickerId}", insight.Id, tickerId);
                return savedInsight ?? insight;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI insight for ticker {TickerId}", tickerId);
            throw;
        }
    }

    public async Task<IEnumerable<AIInsight>> GenerateInsightsBatchAsync(
        IEnumerable<Guid> tickerIds,
        CancellationToken cancellationToken = default)
    {
        var insights = new List<AIInsight>();
        var tickerIdList = tickerIds.ToList();

        foreach (var tickerId in tickerIdList)
        {
            try
            {
                var insight = await GenerateInsightAsync(tickerId, null, null, null, cancellationToken);
                insights.Add(insight);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate insight for ticker {TickerId}, continuing with others", tickerId);
                // Continue with other tickers
            }
        }

        return insights;
    }

    public async Task DismissInsightAsync(Guid insightId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var insight = await _aiInsightRepository.GetByIdAsync(insightId);
            if (insight == null)
            {
                throw new Domain.Exceptions.NotFoundException("AIInsight", insightId);
            }

            insight.DismissedAt = DateTime.UtcNow;
            insight.DismissedByUserId = userId;
            insight.UpdatedAt = DateTime.UtcNow;

            await _aiInsightRepository.UpdateAsync(insight);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Dismissed AI insight {Id} by user {UserId}", insightId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing AI insight {Id}", insightId);
            throw;
        }
    }

    public async Task<MarketSentiment> GetMarketSentimentAsync(CancellationToken cancellationToken = default)
    {
        var insights = await _aiInsightRepository.GetNonDismissedInsightsAsync(cancellationToken);
        var insightsList = insights.ToList();

            var buyCount = insightsList.Count(i => i.Type == InsightType.Buy);
            var sellCount = insightsList.Count(i => i.Type == InsightType.Sell);
            var holdCount = insightsList.Count(i => i.Type == InsightType.Hold);
            var totalCount = insightsList.Count;

            // Calculate overall sentiment
            string overall = "Neutral";
            int score = MarketSentimentConstants.DEFAULT_SENTIMENT_SCORE;
            double sellPercentage = 0;
            if (totalCount > 0)
            {
                var buyPercentage = (double)buyCount / totalCount * 100;
                sellPercentage = (double)sellCount / totalCount * 100;
                
                score = (int)(buyPercentage - sellPercentage + MarketSentimentConstants.DEFAULT_SENTIMENT_SCORE); // Normalize to 0-100
                score = Math.Max(0, Math.Min(100, score));

                if (buyPercentage > TechnicalIndicatorConstants.BULLISH_MARKET_THRESHOLD)
                    overall = "Bullish";
                else if (sellPercentage > TechnicalIndicatorConstants.BEARISH_MARKET_THRESHOLD)
                    overall = "Bearish";
            }

            // Count opportunities (Buy signals with confidence > threshold)
            var opportunitiesCount = insightsList
                .Count(i => i.Type == InsightType.Buy && i.Confidence > TechnicalIndicatorConstants.HIGH_CONFIDENCE_THRESHOLD);

            // Calculate risk level based on sell signals and volatility
            sellPercentage = totalCount > 0 ? (double)sellCount / totalCount * 100 : 0;
            string riskLevel = "Low";
            int volatilityIndex = MarketSentimentConstants.LOW_VOLATILITY_INDEX;
            
            if (sellPercentage > TechnicalIndicatorConstants.HIGH_RISK_THRESHOLD)
            {
                riskLevel = "High";
                volatilityIndex = MarketSentimentConstants.HIGH_VOLATILITY_INDEX;
            }
            else if (sellPercentage > TechnicalIndicatorConstants.MODERATE_RISK_THRESHOLD)
            {
                riskLevel = "Moderate";
                volatilityIndex = MarketSentimentConstants.MODERATE_VOLATILITY_INDEX;
            }
            else
            {
                riskLevel = "Low";
                volatilityIndex = MarketSentimentConstants.LOW_VOLATILITY_INDEX;
            }

            return new MarketSentiment
            {
                Overall = overall,
                Score = score,
                BuySignalsCount = buyCount,
                SellSignalsCount = sellCount,
                HoldSignalsCount = holdCount,
                OpportunitiesCount = opportunitiesCount,
                RiskLevel = riskLevel,
                VolatilityIndex = volatilityIndex
            };
    }

    public async Task CleanupOldDismissedInsightsAsync(int daysOld = 7, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var oldInsights = await _aiInsightRepository.GetOldDismissedInsightsAsync(cutoffDate, cancellationToken);
            var oldInsightsList = oldInsights.ToList();

            if (oldInsightsList.Any())
            {
                await _aiInsightRepository.DeleteRangeAsync(oldInsightsList, cancellationToken);
                await _aiInsightRepository.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Cleaned up {Count} old dismissed insights", oldInsightsList.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old dismissed insights");
        }
    }
}
