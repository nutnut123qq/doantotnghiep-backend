using System.ServiceModel.Syndication;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Maps RSS <see cref="SyndicationItem"/> rows to <see cref="CorporateEvent"/> when a known ticker appears in the text.
/// </summary>
public static class CorporateEventRssMapper
{
    public static CorporateEvent? TryMapItem(
        SyndicationItem item,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid>? tickerNameAliasMap,
        string feedDisplayName)
    {
        var link = RssSyndicationUtilities.GetItemLink(item);
        if (string.IsNullOrWhiteSpace(link))
            return null;

        var title = item.Title?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
            title = link;

        var summary = RssSyndicationUtilities.GetSummaryText(item);
        var combined = $"{title}\n{summary}";

        if (!CorporateEventTextHelper.TryResolveTickerId(combined, tickerMap, tickerNameAliasMap, out var tickerId))
            return null;

        var published = RssSyndicationUtilities.ResolvePublishedAtUtc(item);
        var eventType = CorporateEventTextHelper.DetermineEventType(combined);

        var ev = CorporateEventTextHelper.CreateEventFromRss(
            tickerId,
            published,
            title,
            string.IsNullOrWhiteSpace(summary) ? null : summary,
            link,
            eventType);

        if (string.IsNullOrWhiteSpace(ev.Description))
            ev.Description = $"Nguồn RSS: {feedDisplayName}";

        return ev;
    }
}
