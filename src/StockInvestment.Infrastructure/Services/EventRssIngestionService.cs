using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HtmlAgilityPack;
using System.Globalization;
using System.Text;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Configuration;
using StockInvestment.Infrastructure.External;

namespace StockInvestment.Infrastructure.Services;

public class EventRssIngestionService : IEventRssIngestionService
{
    private readonly ILogger<EventRssIngestionService> _logger;
    private readonly EventIngestionOptions _options;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HttpClient _httpClient;
    private readonly IAIService _aiService;

    public EventRssIngestionService(
        ILogger<EventRssIngestionService> logger,
        IOptions<EventIngestionOptions> options,
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        IAIService aiService)
    {
        _logger = logger;
        _options = options.Value;
        _unitOfWork = unitOfWork;
        _httpClient = httpClientFactory.CreateClient("EventCrawler");
        _aiService = aiService;
    }

    public async Task<int> IngestFromRssAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Event RSS ingestion is disabled");
            return 0;
        }

        var allSources = _options.Sources
            .Where(s => s.Enabled)
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .OrderBy(s => s.Priority)
            .ToList();

        if (allSources.Count == 0)
        {
            _logger.LogInformation("No event ingestion sources configured under EventIngestion:Sources");
            return 0;
        }

        var tickers = (await _unitOfWork.Repository<StockTicker>().GetAllAsync()).ToList();
        var tickerMap = tickers
            .ToDictionary(t => t.Symbol.ToUpperInvariant(), t => t.Id, StringComparer.OrdinalIgnoreCase);
        var tickerNameAliasMap = BuildTickerNameAliasMap(tickers);
        var idToSymbol = tickers.ToDictionary(t => t.Id, t => t.Symbol);

        var added = 0;
        var maxPerSource = Math.Max(1, _options.MaxItemsPerRun / Math.Max(1, allSources.Count));

        foreach (var source in allSources)
        {
            var maxItems = source.MaxItems ?? maxPerSource;
            maxItems = Math.Min(maxItems, maxPerSource);

            var mappedEvents = await CrawlSourceWithFallbackAsync(
                source,
                tickerMap,
                tickerNameAliasMap,
                maxItems,
                _options.MinItemsBeforeFallback,
                cancellationToken);

            added += await PersistEventsAsync(mappedEvents, idToSymbol, cancellationToken);
        }

        _logger.LogInformation("Event RSS ingestion completed with added={AddedCount}", added);

        return added;
    }

    private async Task<int> PersistEventsAsync(
        IEnumerable<CorporateEvent> events,
        IReadOnlyDictionary<Guid, string> idToSymbol,
        CancellationToken cancellationToken)
    {
        var added = 0;
        foreach (var ev in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var link = ev.SourceUrl ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(link) && await _unitOfWork.CorporateEvents.ExistsBySourceUrlAsync(link))
                continue;

            await _unitOfWork.CorporateEvents.CreateAsync(ev);
            added++;

            if (!idToSymbol.TryGetValue(ev.StockTickerId, out var sym))
                continue;

            await CorporateEventRagHelper.TryIngestForRagAsync(
                _aiService,
                ev,
                sym,
                _logger,
                cancellationToken);
        }
        return added;
    }

    private async Task<IReadOnlyList<CorporateEvent>> CrawlSourceWithFallbackAsync(
        NewsSourceConfig source,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid> tickerNameAliasMap,
        int maxItems,
        int pipelineMinItemsBeforeFallback,
        CancellationToken cancellationToken,
        HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var key = $"{source.Name}|{source.Kind}|{source.Url}|{source.HtmlTemplate}";
        if (!visited.Add(key))
        {
            _logger.LogWarning("Detected recursive event fallback config for source {SourceName}.", source.Name);
            return Array.Empty<CorporateEvent>();
        }

        var fetched = (await CrawlSingleSourceAsync(source, tickerMap, tickerNameAliasMap, maxItems, cancellationToken)).ToList();
        var threshold = Math.Max(0, source.MinItemsBeforeFallback ?? pipelineMinItemsBeforeFallback);
        _logger.LogInformation(
            "Event source {SourceName} fetched={FetchedCount} threshold={Threshold} fallbackCandidates={FallbackCount}",
            source.Name,
            fetched.Count,
            threshold,
            source.FallbackSources.Count);

        if (fetched.Count >= threshold || source.FallbackSources.Count == 0)
        {
            return fetched;
        }

        var aggregate = new List<CorporateEvent>(fetched);
        foreach (var fallback in source.FallbackSources.Where(s => s.Enabled).OrderBy(s => s.Priority))
        {
            var nestedVisited = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
            var fallbackEvents = await CrawlSourceWithFallbackAsync(
                fallback,
                tickerMap,
                tickerNameAliasMap,
                maxItems,
                pipelineMinItemsBeforeFallback,
                cancellationToken,
                nestedVisited);
            aggregate.AddRange(fallbackEvents);

            if (aggregate.Count >= threshold)
                break;
        }

        return aggregate
            .GroupBy(e => new { e.StockTickerId, e.EventType, Day = e.EventDate.Date, Url = e.SourceUrl ?? string.Empty })
            .Select(g => g.First())
            .Take(maxItems)
            .ToList();
    }

    private async Task<IEnumerable<CorporateEvent>> CrawlSingleSourceAsync(
        NewsSourceConfig source,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid> tickerNameAliasMap,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var kind = source.Kind.Trim();
        if (kind.Equals("Rss", StringComparison.OrdinalIgnoreCase))
        {
            return await CrawlRssSourceAsync(source, tickerMap, tickerNameAliasMap, maxItems, cancellationToken);
        }

        if (kind.Equals("HtmlBuiltin", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ResolveEventBuiltin(source);
            if (resolved == null)
            {
                _logger.LogWarning("Unknown Event HtmlBuiltin template {Template} for source {SourceName}", source.HtmlTemplate, source.Name);
                return Array.Empty<CorporateEvent>();
            }

            return await CrawlHtmlSourceAsync(resolved, tickerMap, tickerNameAliasMap, maxItems, cancellationToken);
        }

        if (kind.Equals("HtmlGeneric", StringComparison.OrdinalIgnoreCase))
        {
            return await CrawlHtmlSourceAsync(source, tickerMap, tickerNameAliasMap, maxItems, cancellationToken);
        }

        _logger.LogWarning("Unsupported event source kind {Kind} for source {SourceName}", source.Kind, source.Name);
        return Array.Empty<CorporateEvent>();
    }

    private async Task<IEnumerable<CorporateEvent>> CrawlRssSourceAsync(
        NewsSourceConfig source,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid> tickerNameAliasMap,
        int maxItems,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Url))
            return Array.Empty<CorporateEvent>();

        var feed = await RssFeedLoader.LoadFeedAsync(_httpClient, source.Url, _logger, cancellationToken);
        if (feed == null)
            return Array.Empty<CorporateEvent>();

        var output = new List<CorporateEvent>();
        foreach (var item in feed.Items.Take(maxItems))
        {
            var ev = CorporateEventRssMapper.TryMapItem(item, tickerMap, tickerNameAliasMap, source.Name);
            if (ev != null)
                output.Add(ev);
        }
        return output;
    }

    private async Task<IEnumerable<CorporateEvent>> CrawlHtmlSourceAsync(
        NewsSourceConfig source,
        IReadOnlyDictionary<string, Guid> tickerMap,
        IReadOnlyDictionary<string, Guid> tickerNameAliasMap,
        int maxItems,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Url)
            || string.IsNullOrWhiteSpace(source.ItemXPath)
            || string.IsNullOrWhiteSpace(source.TitleXPath))
        {
            return Array.Empty<CorporateEvent>();
        }

        try
        {
            var html = await _httpClient.GetStringAsync(source.Url, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(source.ItemXPath);
            if (nodes == null || nodes.Count == 0)
            {
                _logger.LogWarning(
                    "Event Html source {SourceName} matched 0 nodes. Url={Url}, ItemXPath={ItemXPath}",
                    source.Name,
                    source.Url,
                    source.ItemXPath);
                return Array.Empty<CorporateEvent>();
            }

            _logger.LogInformation("Event Html source {SourceName} matched {NodeCount} nodes from {Url}", source.Name, nodes.Count, source.Url);

            var events = new List<CorporateEvent>();
            foreach (var node in nodes.Take(maxItems))
            {
                var titleNode = node.SelectSingleNode(source.TitleXPath)
                    ?? node.SelectSingleNode(".//h3[contains(@class,'title-news')]/a")
                    ?? node.SelectSingleNode(".//h2[contains(@class,'title-news')]/a")
                    ?? node.SelectSingleNode(".//a[contains(@href,'.html') or contains(@href,'.htm')]");
                if (titleNode == null)
                    continue;

                var title = HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var summary = string.IsNullOrWhiteSpace(source.SummaryXPath)
                    ? null
                    : HtmlEntity.DeEntitize(node.SelectSingleNode(source.SummaryXPath)?.InnerText ?? string.Empty).Trim();
                var timeText = string.IsNullOrWhiteSpace(source.TimeXPath)
                    ? string.Empty
                    : node.SelectSingleNode(source.TimeXPath)?.InnerText?.Trim() ?? string.Empty;

                var linkNode = string.IsNullOrWhiteSpace(source.LinkXPath)
                    ? titleNode
                    : node.SelectSingleNode(source.LinkXPath) ?? titleNode;
                var link = linkNode?.GetAttributeValue("href", string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(link))
                    continue;
                link = NormalizeLink(link, source.BaseUrl);

                var combined = $"{title}\n{summary}";
                if (!CorporateEventTextHelper.TryResolveTickerId(combined, tickerMap, tickerNameAliasMap, out var tickerId))
                    continue;

                var eventType = CorporateEventTextHelper.DetermineEventType(combined);
                var published = ParseLooseDate(timeText);
                var ev = CorporateEventTextHelper.CreateEventFromRss(
                    tickerId,
                    published,
                    title,
                    string.IsNullOrWhiteSpace(summary) ? null : summary,
                    link,
                    eventType);
                events.Add(ev);
            }

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl HTML event source {SourceName}", source.Name);
            return Array.Empty<CorporateEvent>();
        }
    }

    private static string NormalizeLink(string link, string? baseUrl)
    {
        if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return link;

        if (string.IsNullOrWhiteSpace(baseUrl))
            return link;

        return link.StartsWith('/')
            ? $"{baseUrl.TrimEnd('/')}{link}"
            : $"{baseUrl.TrimEnd('/')}/{link.TrimStart('/')}";
    }

    private static DateTime ParseLooseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DateTime.UtcNow;

        if (DateTime.TryParse(text, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();

        return DateTime.UtcNow;
    }

    private static NewsSourceConfig? ResolveEventBuiltin(NewsSourceConfig source)
    {
        if (string.IsNullOrWhiteSpace(source.HtmlTemplate))
            return null;

        if (source.HtmlTemplate.Equals("TuoiTre", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceConfig
            {
                Name = source.Name,
                Kind = "HtmlGeneric",
                Url = "https://tuoitre.vn/chung-khoan.htm",
                BaseUrl = "https://tuoitre.vn",
                ItemXPath = "//h3[a[contains(@href,'.htm')]]",
                TitleXPath = ".//a",
                LinkXPath = ".//a",
                MaxItems = source.MaxItems
            };
        }

        if (source.HtmlTemplate.Equals("VnExpress", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceConfig
            {
                Name = source.Name,
                Kind = "HtmlGeneric",
                Url = "https://vnexpress.net/kinh-doanh/chung-khoan",
                BaseUrl = "https://vnexpress.net",
                ItemXPath = "//article[contains(@class,'item-news')]",
                TitleXPath = ".//h3[contains(@class,'title-news')]/a | .//h2[contains(@class,'title-news')]/a | .//a[contains(@href,'.html')]",
                LinkXPath = ".//h3[contains(@class,'title-news')]/a | .//h2[contains(@class,'title-news')]/a | .//a[contains(@href,'.html')]",
                SummaryXPath = ".//p[contains(@class,'description')]",
                MaxItems = source.MaxItems
            };
        }

        return null;
    }

    private static IReadOnlyDictionary<string, Guid> BuildTickerNameAliasMap(IEnumerable<StockTicker> tickers)
    {
        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var ticker in tickers)
        {
            if (string.IsNullOrWhiteSpace(ticker.Name))
                continue;

            AddAlias(NormalizeAlias(ticker.Name), ticker.Id, map);

            var simplified = ticker.Name
                .Replace("công ty cổ phần", "", StringComparison.OrdinalIgnoreCase)
                .Replace("ctcp", "", StringComparison.OrdinalIgnoreCase)
                .Replace("tập đoàn", "", StringComparison.OrdinalIgnoreCase)
                .Replace("ngân hàng", "", StringComparison.OrdinalIgnoreCase)
                .Replace("thương mại cổ phần", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            AddAlias(NormalizeAlias(simplified), ticker.Id, map);
        }

        return map;
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
        return System.Text.RegularExpressions.Regex
            .Replace(noMarks, @"[\p{P}\p{S}\s]+", " ")
            .Trim();
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
}
