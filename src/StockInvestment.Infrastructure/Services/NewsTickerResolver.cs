using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.External;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Resolves stock tickers from news article text (symbol tokens + company-name aliases).
/// </summary>
public static class NewsTickerResolver
{
    public static IReadOnlyDictionary<string, Guid> BuildTickerMap(IEnumerable<StockTicker> tickers)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var ticker in tickers)
        {
            if (string.IsNullOrWhiteSpace(ticker.Symbol))
                continue;

            var sym = ticker.Symbol.Trim().ToUpperInvariant();
            if (!map.ContainsKey(sym))
                map[sym] = ticker.Id;
        }

        return map;
    }

    public static IReadOnlyDictionary<string, Guid> BuildTickerNameAliasMap(IEnumerable<StockTicker> tickers)
    {
        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var ticker in tickers)
        {
            if (string.IsNullOrWhiteSpace(ticker.Name))
                continue;

            AddAlias(NormalizeAlias(ticker.Name), ticker.Id, map);

            var simplified = ticker.Name
                .Replace("công ty c? ph?n", "", StringComparison.OrdinalIgnoreCase)
                .Replace("ctcp", "", StringComparison.OrdinalIgnoreCase)
                .Replace("t?p ?oàn", "", StringComparison.OrdinalIgnoreCase)
                .Replace("ngân hàng", "", StringComparison.OrdinalIgnoreCase)
                .Replace("th??ng m?i c? ph?n", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            AddAlias(NormalizeAlias(simplified), ticker.Id, map);
        }

        return map;
    }

    /// <summary>
    /// Phrases suitable for ILike search when fetching news for a single ticker.
    /// </summary>
    public static IReadOnlyList<string> GetSearchPhrasesForTicker(StockTicker ticker)
    {
        var phrases = new List<string>();
        if (!string.IsNullOrWhiteSpace(ticker.Symbol))
            phrases.Add(ticker.Symbol.Trim().ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(ticker.Name))
        {
            phrases.Add(ticker.Name.Trim());
            var simplified = ticker.Name
                .Replace("công ty c? ph?n", "", StringComparison.OrdinalIgnoreCase)
                .Replace("ctcp", "", StringComparison.OrdinalIgnoreCase)
                .Replace("t?p ?oàn", "", StringComparison.OrdinalIgnoreCase)
                .Replace("ngân hàng", "", StringComparison.OrdinalIgnoreCase)
                .Replace("th??ng m?i c? ph?n", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            if (!string.IsNullOrWhiteSpace(simplified) && !phrases.Contains(simplified, StringComparer.OrdinalIgnoreCase))
                phrases.Add(simplified);
        }

        return phrases
            .Where(p => p.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string CombineNewsText(News news)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(news.Title))
            sb.AppendLine(news.Title);
        if (!string.IsNullOrWhiteSpace(news.Summary))
            sb.AppendLine(news.Summary);
        if (!string.IsNullOrWhiteSpace(news.Content))
            sb.AppendLine(news.Content);
        return sb.ToString();
    }

    public static bool TryResolveTickerId(
        News news,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid>? tickerNameAliasMap,
        out Guid tickerId)
    {
        return CorporateEventTextHelper.TryResolveTickerId(
            CombineNewsText(news),
            tickerMap,
            tickerNameAliasMap,
            out tickerId);
    }

    public static void ApplyTickerTags(
        IEnumerable<News> newsItems,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid>? tickerNameAliasMap)
    {
        foreach (var news in newsItems)
        {
            if (news.TickerId.HasValue)
                continue;

            if (TryResolveTickerId(news, tickerMap, tickerNameAliasMap, out var tickerId))
                news.TickerId = tickerId;
        }
    }

    private static void AddAlias(string alias, Guid tickerId, IDictionary<string, Guid> map)
    {
        if (alias.Length < 6)
            return;

        if (!map.ContainsKey(alias))
            map[alias] = tickerId;
    }

    private static string NormalizeAlias(string text)
    {
        var noMarks = RemoveDiacritics(text).ToLowerInvariant();
        return Regex.Replace(noMarks, @"[\p{P}\p{S}\s]+", " ").Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
