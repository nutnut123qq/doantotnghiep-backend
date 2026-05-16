using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
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

        return CorporateEventType.Unknown;
    }

    /// <summary>
    /// Attempts to extract an explicit event date from Vietnamese title/description text
    /// (e.g. "ngày 15/05/2026", "15-05-2026", "15/05").
    /// </summary>
    public static DateTime? TryParseEventDateFromText(string text, DateTime? referenceYear = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var year = referenceYear?.Year ?? DateTime.UtcNow.Year;

        // Full date: dd/MM/yyyy or dd-MM-yyyy
        var m = Regex.Match(text, @"\b(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\b");
        if (m.Success
            && int.TryParse(m.Groups[1].Value, out var d1)
            && int.TryParse(m.Groups[2].Value, out var mo1)
            && int.TryParse(m.Groups[3].Value, out var y1))
        {
            if (y1 >= 2000 && y1 <= 2100 && mo1 <= 12 && d1 <= 31)
            {
                try { return new DateTime(y1, mo1, d1, 0, 0, 0, DateTimeKind.Utc); } catch { }
            }
        }

        // Partial date: dd/MM (assume current year)
        m = Regex.Match(text, @"\b(\d{1,2})[\/\-](\d{1,2})\b");
        if (m.Success
            && int.TryParse(m.Groups[1].Value, out var d2)
            && int.TryParse(m.Groups[2].Value, out var mo2))
        {
            if (mo2 <= 12 && d2 <= 31)
            {
                try { return new DateTime(year, mo2, d2, 0, 0, 0, DateTimeKind.Utc); } catch { }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the first stock symbol in <paramref name="text"/> that exists in <paramref name="tickerMap"/>.
    /// </summary>
    public static bool TryResolveTickerId(string text, IReadOnlyDictionary<string, Guid> tickerMap, out Guid tickerId)
        => TryResolveTickerId(text, tickerMap, null, out tickerId);

    /// <summary>
    /// Resolves ticker by symbol first, then by company-name aliases.
    /// </summary>
    public static bool TryResolveTickerId(
        string text,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid>? tickerNameAliasMap,
        out Guid tickerId)
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

        if (tickerNameAliasMap is not { Count: > 0 })
            return false;

        var normalizedText = NormalizeForAliasMatch(text);
        foreach (var alias in tickerNameAliasMap)
        {
            if (normalizedText.Contains(alias.Key, StringComparison.Ordinal))
            {
                tickerId = alias.Value;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeForAliasMatch(string text)
    {
        var lower = RemoveDiacritics(text).ToLowerInvariant();
        var compact = Regex.Replace(lower, @"[\p{P}\p{S}\s]+", " ").Trim();
        return compact;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static CorporateEvent CreateEventFromRss(
        Guid stockTickerId,
        DateTime eventDateUtc,
        string title,
        string? description,
        string sourceUrl,
        CorporateEventType eventType)
    {
        if (eventType == CorporateEventType.Unknown)
            throw new ArgumentException("Cannot create a corporate event with Unknown type.", nameof(eventType));

        var combined = title + " " + (description ?? "");

        // Prefer explicit event date found in title/description over RSS pubDate
        var explicitDate = TryParseEventDateFromText(combined);
        var date = explicitDate?.Date ?? eventDateUtc.Date;

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

