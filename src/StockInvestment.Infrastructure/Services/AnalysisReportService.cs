using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// P2-1: Service implementation for Analysis Reports
/// </summary>
public class AnalysisReportService : IAnalysisReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalysisReportService> _logger;

    public AnalysisReportService(
        IUnitOfWork unitOfWork,
        ILogger<AnalysisReportService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<(IEnumerable<AnalysisReportListDto> Items, int Total)> GetReportsBySymbolAsync(
        string symbol,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<AnalysisReport>();
        
        var query = repository.FindAsync(r => r.Symbol == symbol, cancellationToken);
        var allReports = await query;
        var reportsList = allReports.OrderByDescending(r => r.PublishedAt).ToList();

        var total = reportsList.Count;
        var items = reportsList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AnalysisReportListDto
            {
                Id = r.Id,
                Symbol = r.Symbol,
                Title = r.Title,
                FirmName = r.FirmName,
                PublishedAt = r.PublishedAt,
                Recommendation = r.Recommendation,
                TargetPrice = r.TargetPrice,
                SourceUrl = r.SourceUrl,
                ContentPreview = Cap(r.Content, 200)
            }).ToList();

        return (items, total);
    }

    public async Task<AnalysisReport?> GetReportByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Repository<AnalysisReport>().GetByIdAsync(id, cancellationToken);
    }

    public async Task<AnalysisReport> CreateReportAsync(AnalysisReport report, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Repository<AnalysisReport>().AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return report;
    }

    private static string Cap(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "…";
    }
}
