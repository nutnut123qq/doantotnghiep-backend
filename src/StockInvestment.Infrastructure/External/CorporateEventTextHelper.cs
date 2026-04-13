using System.Text.RegularExpressions;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Keyword-based event typing and lightweight construction for RSS-sourced corporate events.
/// </summary>
public static class CorporateEventTextHelper
{
    public static CorporateEventType DetermineEventType(string text)
    {
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("họp đại hội", StringComparison.Ordinal) ||
            lowerText.Contains("agm", StringComparison.Ordinal) ||
            lowerText.Contains("đhđcđ", StringComparison.Ordinal))
            return CorporateEventType.AGM;

        if (lowerText.Contains("cổ tức", StringComparison.Ordinal) ||
            lowerText.Contains("dividend", StringComparison.Ordinal) ||
            lowerText.Contains("trả cổ tức", StringComparison.Ordinal))
            return CorporateEventType.Dividend;

        if (lowerText.Contains("kết quả", StringComparison.Ordinal) ||
            lowerText.Contains("earnings", StringComparison.Ordinal) ||
            lowerText.Contains("lợi nhuận", StringComparison.Ordinal) ||
            lowerText.Contains("doanh thu", StringComparison.Ordinal))
            return CorporateEventType.Earnings;

        if (lowerText.Contains("chia tách", StringComparison.Ordinal) ||
            lowerText.Contains("split", StringComparison.Ordinal) ||
            lowerText.Contains("ghép cổ phiếu", StringComparison.Ordinal))
            return CorporateEventType.StockSplit;

        if (lowerText.Contains("phát hành", StringComparison.Ordinal) ||
            lowerText.Contains("rights issue", StringComparison.Ordinal) ||
            lowerText.Contains("tăng vốn", StringComparison.Ordinal))
            return CorporateEventType.RightsIssue;

        return CorporateEventType.Earnings;
    }

    /// <summary>
    /// Resolves the first stock symbol in <paramref name="text"/> that exists in <paramref name="tickerMap"/>.
    /// </summary>
    public static bool TryResolveTickerId(string text, IReadOnlyDictionary<string, Guid> tickerMap, out Guid tickerId)
    {
        tickerId = default;
        if (string.IsNullOrWhiteSpace(text) || tickerMap.Count == 0)
            return false;

        var upper = text.ToUpperInvariant();
        foreach (Match m in Regex.Matches(upper, @"\b([A-Z]{3,5})\b"))
        {
            var sym = m.Groups[1].Value;
            if (tickerMap.TryGetValue(sym, out tickerId))
                return true;
        }

        return false;
    }

    public static CorporateEvent CreateEventFromRss(
        Guid stockTickerId,
        DateTime eventDateUtc,
        string title,
        string? description,
        string sourceUrl,
        CorporateEventType eventType)
    {
        var combined = title + " " + (description ?? "");
        var date = eventDateUtc.Date;
        var status = date < DateTime.UtcNow.Date
            ? EventStatus.Past
            : date == DateTime.UtcNow.Date
                ? EventStatus.Today
                : EventStatus.Upcoming;

        CorporateEvent ev = eventType switch
        {
            CorporateEventType.Dividend => new DividendEvent
            {
                StockTickerId = stockTickerId,
                EventDate = date,
                Title = title,
                Description = description,
                SourceUrl = sourceUrl,
                Status = status
            },
            CorporateEventType.StockSplit => new StockSplitEvent
            {
                StockTickerId = stockTickerId,
                EventDate = date,
                Title = title,
                Description = description,
                SourceUrl = sourceUrl,
                Status = status,
                SplitRatio = "1:1",
                EffectiveDate = date,
                IsReverseSplit = combined.Contains("ghép", StringComparison.OrdinalIgnoreCase)
            },
            CorporateEventType.AGM => new AGMEvent
            {
                StockTickerId = stockTickerId,
                EventDate = date,
                Title = title,
                Description = description,
                SourceUrl = sourceUrl,
                Status = status,
                Year = date.Year
            },
            CorporateEventType.RightsIssue => new RightsIssueEvent
            {
                StockTickerId = stockTickerId,
                EventDate = date,
                Title = title,
                Description = description,
                SourceUrl = sourceUrl,
                Status = status
            },
            _ => new EarningsEvent
            {
                StockTickerId = stockTickerId,
                EventDate = date,
                Title = title,
                Description = description,
                SourceUrl = sourceUrl,
                Status = status,
                Year = date.Year,
                Period = ExtractPeriodFromText(combined)
            }
        };

        return ev;
    }

    private static string ExtractPeriodFromText(string text)
    {
        var m = Regex.Match(text, @"Q\s*(\d)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out _))
            return $"Q{m.Groups[1].Value}";

        m = Regex.Match(text, @"quý\s*(\d)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out _))
            return $"Q{m.Groups[1].Value}";

        if (text.Contains("năm", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("year", StringComparison.OrdinalIgnoreCase))
            return "Year";

        return "Q1";
    }
}

