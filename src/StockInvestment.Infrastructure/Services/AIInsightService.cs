using System.Net.Http;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.DTOs.AIInsights;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Domain.Constants;
using StockInvestment.Infrastructure.Configuration;

namespace StockInvestment.Infrastructure.Services;

public class AIInsightService : IAIInsightService
{
    private static readonly ConcurrentQueue<(Guid TickerId, string Reason)> RefreshQueue = new();
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIInsightRepository _aiInsightRepository;
    private readonly IStockTickerRepository _stockTickerRepository;
    private readonly IAIService _aiService;
    private readonly ITechnicalDataService _technicalDataService;
    private readonly INewsService _newsService;
    private readonly ILogger<AIInsightService> _logger;
    private readonly AIInsightGenerationOptions _generationOptions;

    public AIInsightService(
        IUnitOfWork unitOfWork,
        IAIInsightRepository aiInsightRepository,
        IStockTickerRepository stockTickerRepository,
        IAIService aiService,
        ITechnicalDataService technicalDataService,
        INewsService newsService,
        ILogger<AIInsightService> logger,
        IOptions<AIInsightGenerationOptions> generationOptions)
    {
        _unitOfWork = unitOfWork;
        _aiInsightRepository = aiInsightRepository;
        _stockTickerRepository = stockTickerRepository;
        _aiService = aiService;
        _technicalDataService = technicalDataService;
        _newsService = newsService;
        _logger = logger;
        _generationOptions = generationOptions.Value;
    }

    public async Task<IEnumerable<AIInsight>> GetInsightsAsync(
        InsightType? type = null,
        string? symbol = null,
        bool includeDismissed = false,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (includeDismissed)
        {
            return await _aiInsightRepository.GetInsightsAsync(type, symbol, includeDismissed, includeDeleted, cancellationToken);
        }

        return await _aiInsightRepository.GetGlobalLatestInsightsAsync(type, symbol, includeDeleted, cancellationToken);
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

            var qualityResult = EvaluateQuality(aiResult, technicalData, sentimentData, ticker.CurrentPrice);
            var enrichedReasoning = BuildEnrichedReasoning(aiResult, qualityResult, technicalData, sentimentData, ticker);
            var adjustedConfidence = Math.Min(aiResult.Confidence, qualityResult.AdjustedConfidence);

            // Check if insight already exists for this ticker and type
            var existingInsight = await _aiInsightRepository.FindActiveInsightAsync(tickerId, insightType, cancellationToken);

            // Serialize reasoning to JSON
            var reasoningJson = JsonSerializer.Serialize(enrichedReasoning);

            if (existingInsight != null)
            {
                // Update existing insight
                existingInsight.Title = aiResult.Title;
                existingInsight.Description = aiResult.Description;
                existingInsight.Confidence = adjustedConfidence;
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
                // Hide any previous visible insights for this ticker so only the newest one remains visible
                await _aiInsightRepository.HidePreviousInsightsAsync(tickerId, cancellationToken: cancellationToken);

                // Create new insight
                var insight = new AIInsight
                {
                    TickerId = tickerId,
                    Type = insightType,
                    Title = aiResult.Title,
                    Description = aiResult.Description,
                    Confidence = adjustedConfidence,
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
                
                _logger.LogInformation("Created AI insight {Id} for ticker {TickerId}. Previous insights hidden.", insight.Id, tickerId);
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

    public async Task<int> GenerateGlobalInsightsHybridAsync(
        IEnumerable<Guid> scheduledTickerIds,
        int maxGeneratePerRun,
        TimeSpan minInsightTtl,
        CancellationToken cancellationToken = default)
    {
        var generatedCount = 0;
        var skippedByTtl = 0;
        var skippedByBudget = 0;
        var failedCount = 0;
        var now = DateTime.UtcNow;
        var candidates = new List<(Guid TickerId, string Reason)>();
        var queuedSet = new HashSet<Guid>();

        while (RefreshQueue.TryDequeue(out var queued))
        {
            if (queuedSet.Add(queued.TickerId))
            {
                candidates.Add(queued);
            }
        }

        foreach (var tickerId in scheduledTickerIds)
        {
            if (queuedSet.Add(tickerId))
            {
                candidates.Add((tickerId, "scheduled"));
            }
        }

        foreach (var (tickerId, reason) in candidates)
        {
            if (generatedCount >= maxGeneratePerRun)
            {
                skippedByBudget++;
                continue;
            }

            var latestGeneratedAt = await _aiInsightRepository.GetLatestGeneratedAtByTickerAsync(tickerId, cancellationToken);
            if (latestGeneratedAt.HasValue && now - latestGeneratedAt.Value < minInsightTtl)
            {
                skippedByTtl++;
                continue;
            }

            try
            {
                await GenerateInsightAsync(tickerId, null, null, null, cancellationToken);
                generatedCount++;
                _logger.LogInformation("Hybrid insight refresh generated for ticker {TickerId}. Reason={Reason}", tickerId, reason);
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(ex, "Hybrid insight refresh failed for ticker {TickerId}. Reason={Reason}", tickerId, reason);
            }

            if (_generationOptions.InterTickerDelayMs > 0 && generatedCount < maxGeneratePerRun)
            {
                try
                {
                    await Task.Delay(_generationOptions.InterTickerDelayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        var globalFeed = await _aiInsightRepository.GetGlobalLatestInsightsAsync(cancellationToken: cancellationToken);
        var uniqueSymbolsInFeed = globalFeed
            .Select(i => i.TickerId)
            .Distinct()
            .Count();

        _logger.LogInformation(
            "Hybrid insight run summary: candidates={Candidates}, generated={Generated}, skippedByTtl={SkippedByTtl}, skippedByBudget={SkippedByBudget}, failed={Failed}, uniqueSymbolsInFeed={UniqueSymbolsInFeed}",
            candidates.Count,
            generatedCount,
            skippedByTtl,
            skippedByBudget,
            failedCount,
            uniqueSymbolsInFeed);

        return generatedCount;
    }

    public Task EnqueueTickerForRefreshAsync(Guid tickerId, string reason, CancellationToken cancellationToken = default)
    {
        RefreshQueue.Enqueue((tickerId, string.IsNullOrWhiteSpace(reason) ? "manual-trigger" : reason));
        _logger.LogInformation("Ticker {TickerId} enqueued for insight refresh. Reason={Reason}", tickerId, reason);
        return Task.CompletedTask;
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

    public async Task SetDeletedStatusAsync(Guid insightId, bool isDeleted, Guid userId, CancellationToken cancellationToken = default)
    {
        var insight = await _aiInsightRepository.GetByIdAsync(insightId);
        if (insight == null)
        {
            throw new Domain.Exceptions.NotFoundException("AIInsight", insightId);
        }

        insight.IsDeleted = isDeleted;
        insight.UpdatedAt = DateTime.UtcNow;
        if (isDeleted)
        {
            insight.DismissedAt ??= DateTime.UtcNow;
            insight.DismissedByUserId ??= userId;
        }

        await _aiInsightRepository.UpdateAsync(insight);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<MarketSentiment> GetMarketSentimentAsync(CancellationToken cancellationToken = default)
    {
        var insights = await _aiInsightRepository.GetNonDismissedInsightsAsync(cancellationToken);
        var insightsList = insights
            .Where(i => ParseMetadata(i.Reasoning).GetValueOrDefault("quality_status", "needs_review") == "approved")
            .ToList();

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

    public async Task<InsightAccuracyMetricsDto> EvaluateAccuracyAsync(int maxInsights = 500, CancellationToken cancellationToken = default)
    {
        var insights = (await _aiInsightRepository.GetInsightsAsync(includeDismissed: true, cancellationToken: cancellationToken))
            .Take(maxInsights)
            .Where(i => i.Ticker != null)
            .ToList();

        var metrics = new InsightAccuracyMetricsDto
        {
            TotalInsightsConsidered = insights.Count
        };

        var now = DateTime.UtcNow;
        decimal calibrationError = 0;
        int calibrationSamples = 0;

        foreach (var insight in insights)
        {
            var metadata = ParseMetadata(insight.Reasoning);
            if (!metadata.TryGetValue("baseline_price", out var baselinePriceRaw) ||
                !decimal.TryParse(baselinePriceRaw, out var baselinePrice) ||
                baselinePrice <= 0)
            {
                continue;
            }

            var currentPrice = insight.Ticker.CurrentPrice;
            if (currentPrice <= 0)
            {
                continue;
            }

            var daysSinceGenerated = (now - insight.GeneratedAt).TotalDays;
            var actualSignal = IsPredictionCorrect(insight.Type, baselinePrice, currentPrice);

            calibrationError += Math.Abs((insight.Confidence / 100m) - (actualSignal ? 1m : 0m));
            calibrationSamples++;

            if (daysSinceGenerated >= 1)
            {
                UpdateHorizon(metrics.TPlus1, insight.Type, baselinePrice, currentPrice);
            }

            if (daysSinceGenerated >= 5)
            {
                UpdateHorizon(metrics.TPlus5, insight.Type, baselinePrice, currentPrice);
            }

            if (daysSinceGenerated >= 20)
            {
                UpdateHorizon(metrics.TPlus20, insight.Type, baselinePrice, currentPrice);
            }
        }

        if (calibrationSamples > 0)
        {
            metrics.ConfidenceCalibrationError = Math.Round(calibrationError / calibrationSamples, 4);
        }

        return metrics;
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

    private static List<string> BuildEnrichedReasoning(
        InsightResult aiResult,
        QualityEvaluation quality,
        Dictionary<string, string>? technicalData,
        Dictionary<string, string>? sentimentData,
        StockTicker ticker)
    {
        var reasoning = aiResult.Reasoning?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? new List<string>();
        var evidence = aiResult.Evidence?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

        if (!evidence.Any())
        {
            evidence.Add($"CurrentPrice={ticker.CurrentPrice}");
            if (technicalData != null)
            {
                foreach (var key in new[] { "rsi", "macd", "sma20", "sma50", "volume" })
                {
                    if (technicalData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        evidence.Add($"{key.ToUpperInvariant()}={value}");
                    }
                }
            }
            if (sentimentData != null && sentimentData.TryGetValue("score", out var score))
            {
                evidence.Add($"NEWS_SENTIMENT_SCORE={score}");
            }
        }

        foreach (var item in evidence.Take(5))
        {
            reasoning.Add($"[EVIDENCE] {item}");
        }

        reasoning.Add($"[QUALITY] status={quality.Status};score={quality.Score};adjusted_confidence={quality.AdjustedConfidence}");
        reasoning.Add($"[META] baseline_price={ticker.CurrentPrice}");
        reasoning.Add($"[META] data_generated_at={DateTime.UtcNow:O}");
        reasoning.Add($"[META] quality_status={quality.Status}");
        reasoning.Add($"[META] quality_score={quality.Score}");

        if (aiResult.Metadata != null)
        {
            foreach (var kvp in aiResult.Metadata)
            {
                reasoning.Add($"[META] {kvp.Key}={kvp.Value}");
            }
        }

        return reasoning;
    }

    private static QualityEvaluation EvaluateQuality(
        InsightResult aiResult,
        Dictionary<string, string>? technicalData,
        Dictionary<string, string>? sentimentData,
        decimal currentPrice)
    {
        var score = 50;

        if (!string.IsNullOrWhiteSpace(aiResult.Title)) score += 5;
        if (!string.IsNullOrWhiteSpace(aiResult.Description)) score += 5;
        if (aiResult.Reasoning?.Count >= 2) score += 10;
        if (aiResult.Evidence?.Any() == true) score += 10;
        if (currentPrice > 0) score += 5;

        if (technicalData != null)
        {
            foreach (var key in new[] { "rsi", "macd", "volume" })
            {
                if (technicalData.ContainsKey(key)) score += 4;
            }
        }

        if (sentimentData != null && sentimentData.ContainsKey("score"))
        {
            score += 8;
        }

        if (aiResult.Confidence is < 0 or > 100)
        {
            score -= 20;
        }

        var status = score >= 80 ? "approved" : score >= 60 ? "needs_review" : "rejected";
        var adjustedConfidence = status switch
        {
            "approved" => aiResult.Confidence,
            "needs_review" => Math.Max(35, aiResult.Confidence - 15),
            _ => Math.Max(20, aiResult.Confidence - 35)
        };

        return new QualityEvaluation(status, Math.Clamp(score, 0, 100), adjustedConfidence);
    }

    private static Dictionary<string, string> ParseMetadata(string reasoningJson)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var reasoning = JsonSerializer.Deserialize<List<string>>(reasoningJson) ?? new List<string>();
        foreach (var line in reasoning)
        {
            if (!line.StartsWith("[META] ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line[7..];
            var separator = payload.IndexOf('=');
            if (separator <= 0 || separator == payload.Length - 1)
            {
                continue;
            }

            var key = payload[..separator].Trim();
            var value = payload[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }
        return result;
    }

    private static bool IsPredictionCorrect(InsightType type, decimal baselinePrice, decimal currentPrice)
    {
        var changePct = baselinePrice == 0 ? 0 : (currentPrice - baselinePrice) / baselinePrice * 100m;
        return type switch
        {
            InsightType.Buy => changePct >= 0,
            InsightType.Sell => changePct <= 0,
            _ => Math.Abs(changePct) <= 2m
        };
    }

    private static void UpdateHorizon(HorizonMetricDto metric, InsightType type, decimal baselinePrice, decimal currentPrice)
    {
        metric.EligibleInsights++;
        var correct = IsPredictionCorrect(type, baselinePrice, currentPrice);
        if (correct)
        {
            metric.CorrectPredictions++;
        }
        else
        {
            metric.FalseSignals++;
        }
    }

    private sealed record QualityEvaluation(string Status, int Score, int AdjustedConfidence);
}
