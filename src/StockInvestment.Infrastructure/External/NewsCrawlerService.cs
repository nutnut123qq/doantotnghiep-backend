using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.External;

/// <summary>
/// Service to crawl news from Vietnamese financial news sources
/// </summary>
public class NewsCrawlerService : INewsCrawlerService
{
    private readonly ILogger<NewsCrawlerService> _logger;
    private readonly HttpClient _httpClient;

    public NewsCrawlerService(
        ILogger<NewsCrawlerService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("NewsCrawler");
    }

    public async Task<IEnumerable<News>> CrawlNewsAsync(int maxArticles = 20)
    {
        var allNews = new List<News>();

        try
        {
            // Crawl from multiple sources in parallel
            var tasks = new[]
            {
                CrawlFromSourceAsync("CafeF", maxArticles / 3),
                CrawlFromSourceAsync("VNExpress", maxArticles / 3),
                CrawlFromSourceAsync("VietStock", maxArticles / 3)
            };

            var results = await Task.WhenAll(tasks);
            
            foreach (var newsItems in results)
            {
                allNews.AddRange(newsItems);
            }

            return allNews.OrderByDescending(n => n.PublishedAt).Take(maxArticles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling news from all sources");
            return allNews;
        }
    }

    public async Task<IEnumerable<News>> CrawlNewsBySymbolAsync(string symbol, int maxArticles = 10)
    {
        var allNews = new List<News>();

        try
        {
            // Search for news containing the stock symbol
            var tasks = new[]
            {
                CrawlCafeFBySymbolAsync(symbol, maxArticles / 2),
                CrawlVietStockBySymbolAsync(symbol, maxArticles / 2)
            };

            var results = await Task.WhenAll(tasks);
            
            foreach (var newsItems in results)
            {
                allNews.AddRange(newsItems);
            }

            return allNews.OrderByDescending(n => n.PublishedAt).Take(maxArticles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling news for symbol {Symbol}", symbol);
            return allNews;
        }
    }

    public async Task<IEnumerable<News>> CrawlFromSourceAsync(string source, int maxArticles = 20)
    {
        return source.ToLower() switch
        {
            "cafef" => await CrawlCafeFAsync(maxArticles),
            "vnexpress" => await CrawlVNExpressAsync(maxArticles),
            "vietstock" => await CrawlVietStockAsync(maxArticles),
            _ => Enumerable.Empty<News>()
        };
    }

    private async Task<IEnumerable<News>> CrawlCafeFAsync(int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = "https://cafef.vn/thi-truong-chung-khoan.chn";
            _logger.LogDebug("Crawling CafeF from URL: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CafeF returned status {StatusCode} for URL: {Url}. Skipping this source.", response.StatusCode, url);
                return newsList;
            }
            
            var html = await response.Content.ReadAsStringAsync();
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse CafeF news structure
            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='tlitem']");
            
            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='sapo']");
                    var timeNode = node.SelectSingleNode(".//span[@class='time']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";
                    var timeText = timeNode?.InnerText.Trim() ?? "";

                    // Make URL absolute
                    if (!articleUrl.StartsWith("http"))
                    {
                        articleUrl = "https://cafef.vn" + articleUrl;
                    }

                    var news = new News
                    {
                        Title = title,
                        Content = description,
                        Source = "CafeF",
                        Url = articleUrl,
                        PublishedAt = ParseCafeFTime(timeText),
                        CreatedAt = DateTime.UtcNow
                    };

                    newsList.Add(news);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CafeF news item");
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogWarning("HTTP error crawling CafeF: {Message}. This source may be temporarily unavailable.", httpEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling CafeF");
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlVNExpressAsync(int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = "https://vnexpress.net/kinh-doanh/chung-khoan";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse VNExpress news structure
            var newsNodes = doc.DocumentNode.SelectNodes("//article[@class='item-news']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3[@class='title-news']/a");
                    var descNode = node.SelectSingleNode(".//p[@class='description']");
                    var timeNode = node.SelectSingleNode(".//span[@class='time']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";
                    var timeText = timeNode?.InnerText.Trim() ?? "";

                    var news = new News
                    {
                        Title = title,
                        Content = description,
                        Source = "VNExpress",
                        Url = articleUrl,
                        PublishedAt = ParseVNExpressTime(timeText),
                        CreatedAt = DateTime.UtcNow
                    };

                    newsList.Add(news);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing VNExpress news item");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling VNExpress");
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlVietStockAsync(int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = "https://vietstock.vn/chung-khoan.htm";
            _logger.LogDebug("Crawling VietStock from URL: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("VietStock returned status {StatusCode} for URL: {Url}. Skipping this source.", response.StatusCode, url);
                return newsList;
            }
            
            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse VietStock news structure
            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='news-item']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='news-summary']");
                    var timeNode = node.SelectSingleNode(".//span[@class='news-time']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";
                    var timeText = timeNode?.InnerText.Trim() ?? "";

                    // Make URL absolute
                    if (!articleUrl.StartsWith("http"))
                    {
                        articleUrl = "https://vietstock.vn" + articleUrl;
                    }

                    var news = new News
                    {
                        Title = title,
                        Content = description,
                        Source = "VietStock",
                        Url = articleUrl,
                        PublishedAt = ParseVietStockTime(timeText),
                        CreatedAt = DateTime.UtcNow
                    };

                    newsList.Add(news);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing VietStock news item");
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogWarning("HTTP error crawling VietStock: {Message}. This source may be temporarily unavailable.", httpEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling VietStock");
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlCafeFBySymbolAsync(string symbol, int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = $"https://cafef.vn/tim-kiem.chn?keywords={symbol}";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='tlitem']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='sapo']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";

                    if (!articleUrl.StartsWith("http"))
                    {
                        articleUrl = "https://cafef.vn" + articleUrl;
                    }

                    var news = new News
                    {
                        Title = title,
                        Content = description,
                        Source = "CafeF",
                        Url = articleUrl,
                        PublishedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    newsList.Add(news);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CafeF search result");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CafeF for symbol {Symbol}", symbol);
        }

        return newsList;
    }

    private async Task<IEnumerable<News>> CrawlVietStockBySymbolAsync(string symbol, int maxArticles)
    {
        var newsList = new List<News>();

        try
        {
            var url = $"https://vietstock.vn/tim-kiem?q={symbol}";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsNodes = doc.DocumentNode.SelectNodes("//div[@class='news-item']");

            if (newsNodes == null) return newsList;

            foreach (var node in newsNodes.Take(maxArticles))
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    var descNode = node.SelectSingleNode(".//p[@class='news-summary']");

                    if (titleNode == null) continue;

                    var title = titleNode.InnerText.Trim();
                    var articleUrl = titleNode.GetAttributeValue("href", "");
                    var description = descNode?.InnerText.Trim() ?? "";

                    if (!articleUrl.StartsWith("http"))
                    {
                        articleUrl = "https://vietstock.vn" + articleUrl;
                    }

                    var news = new News
                    {
                        Title = title,
                        Content = description,
                        Source = "VietStock",
                        Url = articleUrl,
                        PublishedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    newsList.Add(news);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing VietStock search result");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching VietStock for symbol {Symbol}", symbol);
        }

        return newsList;
    }

    // Helper methods to parse time strings
    private DateTime ParseCafeFTime(string timeText)
    {
        try
        {
            // CafeF format: "10:30 16/12/2024" or "2 giờ trước"
            if (timeText.Contains("giờ trước"))
            {
                var hours = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddHours(-hours);
            }
            else if (timeText.Contains("phút trước"))
            {
                var minutes = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddMinutes(-minutes);
            }
            else if (timeText.Contains("/"))
            {
                return DateTime.Parse(timeText);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return DateTime.UtcNow;
    }

    private DateTime ParseVNExpressTime(string timeText)
    {
        try
        {
            // VNExpress format: "2 giờ trước" or "16/12/2024"
            if (timeText.Contains("giờ trước"))
            {
                var hours = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddHours(-hours);
            }
            else if (timeText.Contains("phút trước"))
            {
                var minutes = int.Parse(Regex.Match(timeText, @"\d+").Value);
                return DateTime.UtcNow.AddMinutes(-minutes);
            }
            else if (timeText.Contains("/"))
            {
                return DateTime.Parse(timeText);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return DateTime.UtcNow;
    }

    private DateTime ParseVietStockTime(string timeText)
    {
        try
        {
            // VietStock format: "16/12/2024 10:30"
            if (!string.IsNullOrEmpty(timeText))
            {
                return DateTime.Parse(timeText);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return DateTime.UtcNow;
    }
}
