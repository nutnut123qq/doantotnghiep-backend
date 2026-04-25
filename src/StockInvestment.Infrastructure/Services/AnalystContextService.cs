using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class AnalystContextService : IAnalystContextService
{
    private readonly INewsService _newsService;
    private readonly IVNStockService _vnStockService;
    private readonly ITechnicalDataService _technicalDataService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalystContextService> _logger;

    public AnalystContextService(
        INewsService newsService,
        IVNStockService vnStockService,
        ITechnicalDataService technicalDataService,
        IUnitOfWork unitOfWork,
        ILogger<AnalystContextService> logger)
    {
        _newsService = newsService;
        _vnStockService = vnStockService;
        _technicalDataService = technicalDataService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<string> BuildNewsContextAsync(
        string symbol,
        int topK,
        int lookbackDays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        var normalized = symbol.Trim().ToUpperInvariant();
        topK = Math.Clamp(topK, 1, 50);
        lookbackDays = Math.Clamp(lookbackDays, 1, 90);

        var items = await _newsService.GetRecentNewsForSymbolAsync(normalized, lookbackDays, topK);
        if (items == null || items.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var n in items)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine($"---");
            sb.AppendLine($"Title: {n.Title}");
            sb.AppendLine($"Published: {n.PublishedAt:O}");
            var body = !string.IsNullOrWhiteSpace(n.Summary) ? n.Summary : "";
            if (!string.IsNullOrWhiteSpace(body))
                sb.AppendLine($"Summary: {body}");
            if (!string.IsNullOrWhiteSpace(n.Url))
                sb.AppendLine($"Url: {n.Url}");
        }

        return sb.ToString().Trim();
    }

    public async Task<string> BuildTechContextAsync(
        string symbol,
        int barLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "Symbol is empty.";

        var normalized = symbol.Trim().ToUpperInvariant();
        if (!Vn30Universe.Contains(normalized))
        {
            return $"Chỉ hỗ trợ mã VN30; '{normalized}' không nằm trong rổ VN30.";
        }

        barLimit = Math.Clamp(barLimit, 1, 500);

        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-Math.Max(barLimit * 3, 120));

        IReadOnlyList<OHLCVData> bars;
        try
        {
            var raw = await _vnStockService.GetHistoricalDataAsync(normalized, startDate, endDate);
            bars = raw?.OrderBy(d => d.Date).ToList() ?? new List<OHLCVData>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historical data failed for {Symbol}", normalized);
            return $"Không lấy được dữ liệu lịch sử giá cho {normalized}: {ex.Message}";
        }

        if (bars.Count == 0)
            return $"Không có dữ liệu OHLCV trong khoảng thời gian đã chọn cho {normalized}.";

        var window = bars.TakeLast(Math.Min(barLimit, bars.Count)).ToList();
        var first = window[0];
        var last = window[^1];
        var periodHigh = window.Max(b => b.High);
        var periodLow = window.Min(b => b.Low);
        var changePct = first.Close != 0
            ? (double)((last.Close - first.Close) / first.Close * 100m)
            : 0d;

        var sb = new StringBuilder();
        sb.AppendLine($"Symbol: {normalized}");
        sb.AppendLine($"Bars (daily, last {window.Count} sessions): from {first.Date:yyyy-MM-dd} to {last.Date:yyyy-MM-dd}");
        sb.AppendLine($"First close: {first.Close.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Last close: {last.Close.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Change % (first→last in window): {changePct.ToString("F2", CultureInfo.InvariantCulture)}%");
        sb.AppendLine($"Period high: {periodHigh.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Period low: {periodLow.ToString(CultureInfo.InvariantCulture)}");

        try
        {
            var tech = await _technicalDataService.PrepareTechnicalDataAsync(normalized, cancellationToken);
            if (tech.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Indicators (snapshot):");
                foreach (var kv in tech)
                    sb.AppendLine($"  {kv.Key}: {kv.Value}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Technical indicators failed for {Symbol}", normalized);
            sb.AppendLine();
            sb.AppendLine($"(Indicators unavailable: {ex.Message})");
        }

        return sb.ToString().Trim();
    }
}
