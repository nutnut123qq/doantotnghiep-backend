using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StockInvestment.Application.Contracts.AI;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;
using StockInvestment.Infrastructure.Services;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.Services;

public class AnalysisReportQAServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAIService> _aiService;
    private readonly Mock<IFinancialReportService> _financialService;
    private readonly Mock<INewsService> _newsService;
    private readonly AnalysisReportQAService _service;

    public AnalysisReportQAServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _aiService = new Mock<IAIService>();
        _financialService = new Mock<IFinancialReportService>();
        _newsService = new Mock<INewsService>();

        var logger = new Mock<ILogger<AnalysisReportQAService>>();
        _service = new AnalysisReportQAService(
            _context,
            _aiService.Object,
            _financialService.Object,
            _newsService.Object,
            logger.Object);
    }

    [Fact]
    public async Task AskQuestionAsync_BuildsContextAndUsesSymbolFilter()
    {
        var report = new AnalysisReport
        {
            Symbol = "ABC",
            Title = "Report ABC",
            FirmName = "Test Firm",
            PublishedAt = new DateTime(2025, 12, 1),
            Recommendation = "Buy",
            TargetPrice = 12345,
            Content = "Report content for ABC",
            SourceUrl = "https://example.com/report"
        };

        _context.AnalysisReports.Add(report);
        await _context.SaveChangesAsync();

        var snapshot = new FinancialSnapshotDto
        {
            Symbol = "ABC",
            Period = "Q4 2025",
            Revenue = 1000m,
            NetProfit = 200m,
            Eps = 1.23m,
            Roe = 12.5m,
            DebtToEquity = 0.5m,
            Notes = "Snapshot notes"
        };

        var newsItems = new List<NewsItemDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "News 1",
                PublishedAt = DateTime.UtcNow.AddDays(-1),
                Url = "https://news.example.com/1",
                Summary = "Summary 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "News 2",
                PublishedAt = DateTime.UtcNow.AddDays(-2),
                Url = "https://news.example.com/2",
                Summary = "Summary 2"
            }
        };

        _financialService
            .Setup(x => x.GetLatestFinancialSnapshotAsync(report.Symbol))
            .ReturnsAsync(snapshot);

        _newsService
            .Setup(x => x.GetRecentNewsForSymbolAsync(report.Symbol, 7, 5))
            .ReturnsAsync(newsItems);

        _aiService
            .Setup(x => x.IngestDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IngestResult
            {
                ChunksUpserted = 1,
                DocumentId = "doc",
                Collection = "rag",
                EmbeddingModel = "test"
            });

        string? capturedBaseContext = null;
        string? capturedDocumentId = null;
        string? capturedSource = null;
        string? capturedSymbol = null;
        int capturedTopK = 0;

        var sources = new List<SourceObject>
        {
            new()
            {
                DocumentId = "finance:ABC:20250101",
                Source = "financial_report",
                Title = "Financial Snapshot ABC",
                SourceUrl = "https://example.com/finance",
                TextPreview = "Preview"
            }
        };

        _aiService
            .Setup(x => x.AnswerQuestionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, string?, string?, int, CancellationToken>(
                (_, baseContext, documentId, source, symbol, topK, _) =>
                {
                    capturedBaseContext = baseContext;
                    capturedDocumentId = documentId;
                    capturedSource = source;
                    capturedSymbol = symbol;
                    capturedTopK = topK;
                })
            .ReturnsAsync(new QuestionAnswerResult
            {
                Answer = "Answer",
                Sources = sources
            });

        var result = await _service.AskQuestionAsync(report.Id, "Question");

        _financialService.Verify(x => x.GetLatestFinancialSnapshotAsync(report.Symbol), Times.Once);
        _newsService.Verify(x => x.GetRecentNewsForSymbolAsync(report.Symbol, 7, 5), Times.Once);

        _aiService.Verify(x => x.IngestDocumentAsync(
            It.Is<string>(id => id.StartsWith("finance:ABC:")),
            "financial_report",
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _aiService.Verify(x => x.IngestDocumentAsync(
            It.Is<string>(id => id.StartsWith("news:ABC:")),
            "news",
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Exactly(newsItems.Count));

        Assert.NotNull(capturedBaseContext);
        Assert.Contains("REPORT CONTEXT", capturedBaseContext);
        Assert.Contains("FINANCIAL SNAPSHOT", capturedBaseContext);
        Assert.Contains("RELATED NEWS", capturedBaseContext);
        Assert.Null(capturedDocumentId);
        Assert.Null(capturedSource);
        Assert.Equal("ABC", capturedSymbol);
        Assert.Equal(8, capturedTopK);

        Assert.Equal("Answer", result.Answer);
        Assert.Single(result.Citations);
        Assert.Equal(sources[0].DocumentId, result.Citations[0].SourceId);
        Assert.Equal(sources[0].Source, result.Citations[0].SourceType);
        Assert.Equal(sources[0].Title, result.Citations[0].Title);
        Assert.Equal(sources[0].SourceUrl, result.Citations[0].Url);
        Assert.Equal(sources[0].TextPreview, result.Citations[0].Excerpt);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
