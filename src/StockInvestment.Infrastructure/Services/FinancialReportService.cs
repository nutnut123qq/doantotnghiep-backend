using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Services;

public class FinancialReportService : IFinancialReportService
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIService _aiService;
    private readonly ILogger<FinancialReportService> _logger;

    public FinancialReportService(
        ApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IAIService aiService,
        ILogger<FinancialReportService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<IEnumerable<FinancialReport>> GetReportsByTickerAsync(Guid tickerId)
    {
        try
        {
            return await _context.FinancialReports
                .Where(r => r.TickerId == tickerId)
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.Quarter)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching financial reports for ticker {TickerId}", tickerId);
            return Enumerable.Empty<FinancialReport>();
        }
    }

    public async Task<IEnumerable<FinancialReport>> GetReportsBySymbolAsync(string symbol)
    {
        try
        {
            var ticker = await _context.StockTickers
                .FirstOrDefaultAsync(t => t.Symbol == symbol.ToUpper());

            if (ticker == null)
            {
                _logger.LogWarning("Ticker {Symbol} not found", symbol);
                return Enumerable.Empty<FinancialReport>();
            }

            return await GetReportsByTickerAsync(ticker.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching financial reports for symbol {Symbol}", symbol);
            return Enumerable.Empty<FinancialReport>();
        }
    }

    public async Task<FinancialReport?> GetReportByIdAsync(Guid id)
    {
        try
        {
            return await _unitOfWork.Repository<FinancialReport>()
                .GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching financial report {Id}", id);
            return null;
        }
    }

    public async Task<FinancialReport> AddReportAsync(FinancialReport report)
    {
        try
        {
            await _unitOfWork.Repository<FinancialReport>().AddAsync(report);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Added financial report {Id} for ticker {TickerId}", report.Id, report.TickerId);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding financial report");
            throw;
        }
    }

    public async Task<IEnumerable<FinancialReport>> AddReportsRangeAsync(IEnumerable<FinancialReport> reports)
    {
        try
        {
            var reportsList = reports.ToList();
            await _unitOfWork.Repository<FinancialReport>().AddRangeAsync(reportsList);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Added {Count} financial reports", reportsList.Count);
            return reportsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding financial reports");
            throw;
        }
    }

    public async Task<string> AskQuestionAsync(Guid reportId, string question)
    {
        try
        {
            var report = await GetReportByIdAsync(reportId);
            if (report == null)
            {
                return "Financial report not found.";
            }

            // Use AI service to answer question based on report content
            var context = $"Financial Report - {report.ReportType} {report.Year}";
            if (report.Quarter.HasValue)
            {
                context += $" Q{report.Quarter}";
            }
            context += $"\n\nData:\n{report.Content}";

            var answer = await _aiService.AnswerQuestionAsync(question, context);
            
            _logger.LogInformation("Answered question for report {ReportId}", reportId);
            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering question for report {ReportId}", reportId);
            return "Error processing your question. Please try again.";
        }
    }
}

