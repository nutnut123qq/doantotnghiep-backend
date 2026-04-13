using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Loads a <see cref="SyndicationFeed"/> from an HTTP RSS/Atom URL (shared by news and event RSS pipelines).
/// </summary>
public static class RssFeedLoader
{
    public static async Task<SyndicationFeed?> LoadFeedAsync(
        HttpClient httpClient,
        string feedUrl,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
            return null;

        try
        {
            using var response = await httpClient.GetAsync(feedUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "RSS fetch returned status {StatusCode} for URL {Url}",
                    response.StatusCode,
                    feedUrl);
                return null;
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(xml))
            {
                logger.LogWarning("RSS feed is empty for URL {Url}", feedUrl);
                return null;
            }

            var trimmed = xml.TrimStart();
            if (trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("RSS URL returned HTML instead of XML: {Url}", feedUrl);
                return null;
            }

            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });

            return SyndicationFeed.Load(reader);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RSS load failed for URL {Url}", feedUrl);
            return null;
        }
    }
}
