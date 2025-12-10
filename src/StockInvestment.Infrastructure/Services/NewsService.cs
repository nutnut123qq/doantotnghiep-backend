using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Services;

public class NewsService : INewsService
{
    private readonly ILogger<NewsService> _logger;
    private readonly List<News> _mockNews;

    public NewsService(ILogger<NewsService> logger)
    {
        _logger = logger;
        _mockNews = GenerateMockNews();
    }

    public Task<IEnumerable<News>> GetNewsAsync(int page = 1, int pageSize = 20, Guid? tickerId = null)
    {
        var result = _mockNews.AsEnumerable();

        if (tickerId.HasValue)
        {
            result = result.Where(n => n.TickerId == tickerId.Value);
        }

        result = result.Skip((page - 1) * pageSize).Take(pageSize);

        return Task.FromResult(result);
    }

    public Task<News?> GetNewsByIdAsync(Guid id)
    {
        var news = _mockNews.FirstOrDefault(n => n.Id == id);
        return Task.FromResult(news);
    }

    public Task RequestSummarizationAsync(Guid newsId)
    {
        // This is handled by the controller via message queue
        return Task.CompletedTask;
    }

    private List<News> GenerateMockNews()
    {
        var news = new List<News>();
        var random = new Random();

        var titles = new[]
        {
            "Company announces strong quarterly earnings",
            "New product launch expected to boost sales",
            "Market analysts upgrade stock rating",
            "Partnership agreement signed with major retailer",
            "Regulatory approval received for new facility",
        };

        var sources = new[] { "VNExpress", "CafeF", "VietStock", "Investing.com" };

        for (int i = 0; i < 20; i++)
        {
            news.Add(new News
            {
                Id = Guid.NewGuid(),
                Title = titles[random.Next(titles.Length)],
                Content = $"This is a sample news article content. {titles[random.Next(titles.Length)]}",
                Source = sources[random.Next(sources.Length)],
                PublishedAt = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
            });
        }

        return news;
    }
}

