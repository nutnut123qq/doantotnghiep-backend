using System.Text.RegularExpressions;
using System.Globalization;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Text.Json;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Service to crawl corporate events from Vietnamese stock market sources
/// </summary>
public class EventCrawlerService : IEventCrawlerService
{
    private readonly ILogger<EventCrawlerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly bool _enableCafeF;

    public EventCrawlerService(
        ILogger<EventCrawlerService> logger,
        IHttpClientFactory httpClientFactory,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("EventCrawler");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _unitOfWork = unitOfWork;
        _enableCafeF = configuration.GetValue<bool>("EventCrawler:EnableCafeF");
    }

    public async Task<IEnumerable<CorporateEvent>> CrawlUpcomingEventsAsync(int daysAhead = 30)
    {
        var allEvents = new List<CorporateEvent>();

        try
        {
            _logger.LogInformation("Starting to crawl upcoming events for next {DaysAhead} days", daysAhead);

            // Crawl from multiple sources in parallel
            var tasks = new List<Task<IEnumerable<CorporateEvent>>>
            {
                CrawlVietStockEventsAsync(daysAhead)
            };

            if (_enableCafeF)
            {
                tasks.Add(CrawlCafeFEventsAsync(daysAhead));
            }
            else
            {
                _logger.LogInformation("CafeF event crawling is disabled by configuration");
            }

            var results = await Task.WhenAll(tasks);
            
            foreach (var events in results)
            {
                allEvents.AddRange(events);
            }

            // Remove duplicates based on ticker, event type, and date
            allEvents = allEvents
                .GroupBy(e => new { e.StockTickerId, e.EventType, e.EventDate.Date })
                .Select(g => g.First())
                .OrderBy(e => e.EventDate)
                .ToList();

            _logger.LogInformation("Successfully crawled {Count} unique events", allEvents.Count);
            return allEvents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling upcoming events");
            return allEvents;
        }
    }

    public async Task<IEnumerable<CorporateEvent>> CrawlEventsBySymbolAsync(string symbol)
    {
        try
        {
            _logger.LogInformation("Crawling events for symbol: {Symbol}", symbol);
            
            // Get ticker by symbol
            var ticker = (await _unitOfWork.Repository<StockTicker>().GetAllAsync())
                .FirstOrDefault(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

            if (ticker == null)
            {
                _logger.LogWarning("Ticker not found for symbol: {Symbol}", symbol);
                return Enumerable.Empty<CorporateEvent>();
            }

            // Crawl events for this ticker
            var events = await CrawlUpcomingEventsAsync(90); // Look 90 days ahead
            return events.Where(e => e.StockTickerId == ticker.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling events for symbol {Symbol}", symbol);
            return Enumerable.Empty<CorporateEvent>();
        }
    }

    public async Task<IEnumerable<CorporateEvent>> CrawlEventsByTypeAsync(CorporateEventType eventType)
    {
        try
        {
            _logger.LogInformation("Crawling events of type: {EventType}", eventType);
            
            var events = await CrawlUpcomingEventsAsync(60);
            return events.Where(e => e.EventType == eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling events of type {EventType}", eventType);
            return Enumerable.Empty<CorporateEvent>();
        }
    }

    private async Task<IEnumerable<CorporateEvent>> CrawlVietStockEventsAsync(int daysAhead)
    {
        var events = new List<CorporateEvent>();

        try
        {
            // VietStock event calendar URL (example - adjust based on actual API/site)
            var url = $"https://finance.vietstock.vn/lich-su-kien";
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch VietStock events. Status: {Status}", response.StatusCode);
                return events;
            }

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse event table (adjust selectors based on actual HTML structure)
            var eventRows = doc.DocumentNode.SelectNodes("//table[@class='table-event']//tr");
            
            if (eventRows == null || !eventRows.Any())
            {
                _logger.LogWarning("No event rows found in VietStock HTML");
                return events;
            }

            foreach (var row in eventRows.Skip(1)) // Skip header
            {
                try
                {
                    var corporateEvent = await ParseVietStockEventRowAsync(row);
                    if (corporateEvent != null)
                    {
                        events.Add(corporateEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing VietStock event row");
                }
            }

            _logger.LogInformation("Crawled {Count} events from VietStock", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling VietStock events");
        }

        return events;
    }

    private async Task<IEnumerable<CorporateEvent>> CrawlCafeFEventsAsync(int daysAhead)
    {
        var events = new List<CorporateEvent>();

        try
        {
            // CafeF event calendar URL (example - adjust based on actual API)
            var url = $"https://cafef.vn/lich-su-kien-niem-yet.chn";
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch CafeF events. Status: {Status}", response.StatusCode);
                return events;
            }

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse event list (adjust selectors based on actual HTML)
            var eventItems = doc.DocumentNode.SelectNodes("//div[@class='event-item']");
            
            if (eventItems == null || !eventItems.Any())
            {
                _logger.LogWarning("No event items found in CafeF HTML");
                return events;
            }

            foreach (var item in eventItems)
            {
                try
                {
                    var corporateEvent = ParseCafeFEventItem(item);
                    if (corporateEvent != null)
                    {
                        events.Add(corporateEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CafeF event item");
                }
            }

            _logger.LogInformation("Crawled {Count} events from CafeF", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling CafeF events");
        }

        return events;
    }

    private async Task<CorporateEvent?> ParseVietStockEventRowAsync(HtmlNode row)
    {
        try
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 4)
                return null;

            // Parse date (e.g., "01/01/2024")
            var dateText = cells[0].InnerText.Trim();
            if (!DateTime.TryParseExact(dateText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate))
            {
                return null;
            }

            // Parse symbol
            var symbol = cells[1].InnerText.Trim();
            var tickers = await _unitOfWork.Repository<StockTicker>().GetAllAsync();
            var ticker = tickers.FirstOrDefault(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

            if (ticker == null)
                return null;

            // Parse event type and details
            var eventTitle = cells[2].InnerText.Trim();
            var eventDetails = cells[3].InnerText.Trim();

            // Determine event type from title/details
            var eventType = DetermineEventType(eventTitle + " " + eventDetails);

            // Create appropriate event based on type
            CorporateEvent corporateEvent = eventType switch
            {
                CorporateEventType.Earnings => CreateEarningsEventFromText(ticker.Id, eventDate, eventTitle, eventDetails),
                CorporateEventType.Dividend => CreateDividendEventFromText(ticker.Id, eventDate, eventTitle, eventDetails),
                CorporateEventType.StockSplit => CreateStockSplitEventFromText(ticker.Id, eventDate, eventTitle, eventDetails),
                CorporateEventType.AGM => CreateAGMEventFromText(ticker.Id, eventDate, eventTitle, eventDetails),
                CorporateEventType.RightsIssue => CreateRightsIssueEventFromText(ticker.Id, eventDate, eventTitle, eventDetails),
                _ => null
            };

            if (corporateEvent != null)
            {
                corporateEvent.SourceUrl = "https://finance.vietstock.vn/lich-su-kien";
                corporateEvent.Status = eventDate.Date < DateTime.Now.Date ? EventStatus.Past :
                                       eventDate.Date == DateTime.Now.Date ? EventStatus.Today :
                                       EventStatus.Upcoming;
            }

            return corporateEvent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing VietStock event row");
            return null;
        }
    }

    private CorporateEvent? ParseCafeFEventItem(HtmlNode item)
    {
        // Similar parsing logic for CafeF
        // This is a placeholder - implement based on actual CafeF HTML structure
        return null;
    }

    private CorporateEventType DetermineEventType(string text)
    {
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("họp đại hội") || lowerText.Contains("agm") || lowerText.Contains("đhđcđ"))
            return CorporateEventType.AGM;
        
        if (lowerText.Contains("cổ tức") || lowerText.Contains("dividend") || lowerText.Contains("trả cổ tức"))
            return CorporateEventType.Dividend;
        
        if (lowerText.Contains("kết quả") || lowerText.Contains("earnings") || lowerText.Contains("lợi nhuận") || lowerText.Contains("doanh thu"))
            return CorporateEventType.Earnings;
        
        if (lowerText.Contains("chia tách") || lowerText.Contains("split") || lowerText.Contains("ghép cổ phiếu"))
            return CorporateEventType.StockSplit;
        
        if (lowerText.Contains("phát hành") || lowerText.Contains("rights issue") || lowerText.Contains("tăng vốn"))
            return CorporateEventType.RightsIssue;

        // Default to Earnings if unclear
        return CorporateEventType.Earnings;
    }

    private EarningsEvent CreateEarningsEventFromText(Guid tickerId, DateTime eventDate, string title, string details)
    {
        var earnings = new EarningsEvent
        {
            StockTickerId = tickerId,
            EventDate = eventDate,
            Title = title,
            Description = details,
            Period = ExtractPeriodFromText(title + " " + details),
            Year = eventDate.Year
        };

        // Try to extract EPS/Revenue if present in details
        var revenueMatch = Regex.Match(details, @"(\d+[\.,]?\d*)\s*(tỷ|triệu|billion|million)", RegexOptions.IgnoreCase);
        if (revenueMatch.Success && decimal.TryParse(revenueMatch.Groups[1].Value.Replace(",", "."), out var revenue))
        {
            earnings.Revenue = revenue * (revenueMatch.Groups[2].Value.ToLower().Contains("tỷ") ? 1_000_000_000 : 1_000_000);
        }

        return earnings;
    }

    private DividendEvent CreateDividendEventFromText(Guid tickerId, DateTime eventDate, string title, string details)
    {
        var dividend = new DividendEvent
        {
            StockTickerId = tickerId,
            EventDate = eventDate,
            Title = title,
            Description = details,
            DividendPerShare = 0
        };

        // Extract dividend amount (e.g., "1,000 VND/share" or "10%")
        var cashMatch = Regex.Match(details, @"(\d+[\.,]?\d*)\s*(vnd|đồng)", RegexOptions.IgnoreCase);
        if (cashMatch.Success && decimal.TryParse(cashMatch.Groups[1].Value.Replace(",", ""), out var cash))
        {
            dividend.CashDividend = cash;
            dividend.DividendPerShare = cash;
        }

        var ratioMatch = Regex.Match(details, @"(\d+)%", RegexOptions.IgnoreCase);
        if (ratioMatch.Success && decimal.TryParse(ratioMatch.Groups[1].Value, out var ratio))
        {
            dividend.StockDividendRatio = ratio / 100m;
        }

        return dividend;
    }

    private StockSplitEvent CreateStockSplitEventFromText(Guid tickerId, DateTime eventDate, string title, string details)
    {
        return new StockSplitEvent
        {
            StockTickerId = tickerId,
            EventDate = eventDate,
            Title = title,
            Description = details,
            SplitRatio = ExtractSplitRatio(details),
            EffectiveDate = eventDate,
            IsReverseSplit = details.ToLower().Contains("ghép") || details.ToLower().Contains("reverse")
        };
    }

    private AGMEvent CreateAGMEventFromText(Guid tickerId, DateTime eventDate, string title, string details)
    {
        return new AGMEvent
        {
            StockTickerId = tickerId,
            EventDate = eventDate,
            Title = title,
            Description = details,
            Year = eventDate.Year,
            Location = ExtractLocation(details)
        };
    }

    private RightsIssueEvent CreateRightsIssueEventFromText(Guid tickerId, DateTime eventDate, string title, string details)
    {
        return new RightsIssueEvent
        {
            StockTickerId = tickerId,
            EventDate = eventDate,
            Title = title,
            Description = details,
            NumberOfShares = 0,
            IssuePrice = 0
        };
    }

    private string ExtractPeriodFromText(string text)
    {
        var quarterMatch = Regex.Match(text, @"Q(\d)|quý\s*(\d)", RegexOptions.IgnoreCase);
        if (quarterMatch.Success)
        {
            return $"Q{quarterMatch.Groups[1].Value}{quarterMatch.Groups[2].Value}";
        }

        if (text.ToLower().Contains("năm") || text.ToLower().Contains("year"))
        {
            return "Year";
        }

        return "Q1"; // Default
    }

    private string ExtractSplitRatio(string text)
    {
        var ratioMatch = Regex.Match(text, @"(\d+):(\d+)");
        if (ratioMatch.Success)
        {
            return $"{ratioMatch.Groups[1].Value}:{ratioMatch.Groups[2].Value}";
        }
        return "1:1";
    }

    private string? ExtractLocation(string text)
    {
        // Look for common location indicators
        var locationMatch = Regex.Match(text, @"tại\s+([^,\.]+)", RegexOptions.IgnoreCase);
        if (locationMatch.Success)
        {
            return locationMatch.Groups[1].Value.Trim();
        }
        return null;
    }
}
