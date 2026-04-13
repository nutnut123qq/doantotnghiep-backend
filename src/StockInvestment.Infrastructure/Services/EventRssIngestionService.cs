using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        var rssSources = _options.Sources
            .Where(s => s.Kind.Equals("Rss", StringComparison.OrdinalIgnoreCase))
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .ToList();

        if (rssSources.Count == 0)
        {
            _logger.LogInformation("No RSS sources configured under EventIngestion:Sources");
            return 0;
        }

        var tickers = (await _unitOfWork.Repository<StockTicker>().GetAllAsync()).ToList();
        var tickerMap = tickers
            .ToDictionary(t => t.Symbol.ToUpperInvariant(), t => t.Id, StringComparer.OrdinalIgnoreCase);
        var idToSymbol = tickers.ToDictionary(t => t.Id, t => t.Symbol);

        var added = 0;
        var maxPerSource = Math.Max(1, _options.MaxItemsPerRun / Math.Max(1, rssSources.Count));

        foreach (var source in rssSources)
        {
            var maxItems = source.MaxItems ?? maxPerSource;
            maxItems = Math.Min(maxItems, maxPerSource);

            var feed = await RssFeedLoader.LoadFeedAsync(
                _httpClient,
                source.Url!,
                _logger,
                cancellationToken);

            if (feed == null)
                continue;

            foreach (var item in feed.Items.Take(maxItems))
            {
                cancellationToken.ThrowIfCancellationRequested();

                CorporateEvent? ev;
                try
                {
                    ev = CorporateEventRssMapper.TryMapItem(item, tickerMap, source.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to map RSS item from {Source}", source.Name);
                    continue;
                }

                if (ev == null)
                    continue;

                var link = ev.SourceUrl!;
                if (await _unitOfWork.CorporateEvents.ExistsBySourceUrlAsync(link))
                    continue;

                if (await _unitOfWork.CorporateEvents.ExistsAsync(ev.StockTickerId, ev.EventType, ev.EventDate))
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
        }

        if (added > 0)
            _logger.LogInformation("Event RSS ingestion added {Count} new events", added);

        return added;
    }
}
