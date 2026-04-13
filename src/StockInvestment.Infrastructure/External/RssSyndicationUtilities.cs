using System.Globalization;
using System.ServiceModel.Syndication;
using System.Xml.Linq;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Shared parsing helpers for <see cref="SyndicationItem"/> (news + event RSS).
/// </summary>
public static class RssSyndicationUtilities
{
    public static string? GetItemLink(SyndicationItem item)
    {
        var alternate = item.Links?.FirstOrDefault(l =>
            string.Equals(l.RelationshipType, "alternate", StringComparison.OrdinalIgnoreCase));
        if (alternate?.Uri != null)
            return alternate.Uri.ToString();

        return item.Links?.FirstOrDefault()?.Uri?.ToString();
    }

    public static string GetSummaryText(SyndicationItem item)
    {
        if (item.Summary is TextSyndicationContent txt)
            return StripHtml(txt.Text)?.Trim() ?? "";

        var s = item.Summary?.ToString();
        return string.IsNullOrWhiteSpace(s) ? "" : StripHtml(s).Trim();
    }

    public static DateTime ResolvePublishedAtUtc(SyndicationItem item)
    {
        try
        {
            var publishDate = item.PublishDate.UtcDateTime;
            if (publishDate != default)
                return publishDate;
        }
        catch
        {
            // continue
        }

        try
        {
            var lastUpdated = item.LastUpdatedTime.UtcDateTime;
            if (lastUpdated != default)
                return lastUpdated;
        }
        catch
        {
            // continue
        }

        var extensionValues = item.ElementExtensions
            .Select(ext =>
            {
                try
                {
                    var x = ext.GetObject<XElement>();
                    return x?.Value;
                }
                catch
                {
                    return null;
                }
            })
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>();

        foreach (var value in extensionValues)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                return parsed.ToUniversalTime();

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedOffset))
                return parsedOffset.UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(noTags);
    }
}
