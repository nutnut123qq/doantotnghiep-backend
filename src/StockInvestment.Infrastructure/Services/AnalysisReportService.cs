using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.AnalysisReports;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// P2-1: Service implementation for Analysis Reports
/// Uses ApplicationDbContext for efficient queries (AsNoTracking) but abstracts it from controllers
/// </summary>
public class AnalysisReportService : IAnalysisReportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalysisReportService> _logger;

    public AnalysisReportService(
        ApplicationDbContext context,
        ILogger<AnalysisReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(IEnumerable<AnalysisReportListDto> Items, int Total)> GetReportsBySymbolAsync(
        string symbol,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // P2-1: Use DbContext with AsNoTracking for efficient read-only pagination
        var query = _context.AnalysisReports
            .AsNoTracking()
            .Where(r => r.Symbol == symbol)
            .OrderByDescending(r => r.PublishedAt);

        var total = await query.CountAsync(cancellationToken);
        var reports = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = reports.Select(r => new AnalysisReportListDto
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
        return await _context.AnalysisReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<AnalysisReport> CreateReportAsync(AnalysisReport report, CancellationToken cancellationToken = default)
    {
        _context.AnalysisReports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);
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
