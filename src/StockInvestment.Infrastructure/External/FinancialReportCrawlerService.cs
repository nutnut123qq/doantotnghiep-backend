using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StockInvestment.Infrastructure.External;

public class FinancialReportCrawlerService : IFinancialReportCrawlerService
{
    private const string VietCapGraphQlUrl = "https://trading.vietcap.com.vn/data-mt/graphql";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FinancialReportCrawlerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly FinancialIngestionOptions _options;

    public FinancialReportCrawlerService(
        IHttpClientFactory httpClientFactory,
        ILogger<FinancialReportCrawlerService> logger,
        IOptions<FinancialIngestionOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("FinancialReportCrawler");
        _options = options.Value;
    }

    public async Task<IEnumerable<FinancialReport>> CrawlReportsBySymbolAsync(string symbol, int maxReports = 10)
    {
        var reports = new List<FinancialReport>();
        var sources = ResolveFinancialSources();

        try
        {
            foreach (var source in sources)
            {
                var sourceItems = await CrawlSourceWithFallbackAsync(
                    source,
                    symbol,
                    maxReports,
                    _options.MinReportsBeforeFallback);

                reports.AddRange(sourceItems);
            }

            reports = DeduplicateReports(reports)
                .Take(maxReports)
                .ToList();

            // Merge supplemental financial data from yfinance (GrossProfit, OperatingProfit, Equity)
            await MergeYFinanceSupplementAsync(symbol, reports);

            _logger.LogInformation("Crawled {Count} financial reports for {Symbol} from {SourceCount} configured sources", reports.Count, symbol, sources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling financial reports for {Symbol}", symbol);
        }

        return reports;
    }

    private async Task MergeYFinanceSupplementAsync(string symbol, List<FinancialReport> reports)
    {
        if (reports.Count == 0)
            return;

        try
        {
            var aiClient = _httpClientFactory.CreateClient("AIService");
            var response = await aiClient.GetAsync($"/financial/yfinance/{symbol}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("YFinance supplement returned {Status} for {Symbol}", response.StatusCode, symbol);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var grossProfit = ReadNullableDecimalFromJson(root, "gross_profit");
            var operatingProfit = ReadNullableDecimalFromJson(root, "operating_profit");
            var equity = ReadNullableDecimalFromJson(root, "equity");
            var totalAssets = ReadNullableDecimalFromJson(root, "total_assets");

            if (grossProfit == null && operatingProfit == null && equity == null)
            {
                _logger.LogInformation("No yfinance supplement data available for {Symbol}", symbol);
                return;
            }

            // Merge into the latest report (most recent ReportDate)
            var latestReport = reports.OrderByDescending(r => r.ReportDate).First();
            var contentDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(latestReport.Content, JsonWriteOptions)
                ?? new Dictionary<string, object?>();

            contentDict["GrossProfit"] = grossProfit;
            contentDict["OperatingProfit"] = operatingProfit;
            contentDict["Equity"] = equity;
            contentDict["TotalAssets"] = totalAssets;
            contentDict["Source"] = $"{contentDict.GetValueOrDefault("Source")?.ToString() ?? "VietCap"}+yfinance";

            latestReport.Content = JsonSerializer.Serialize(contentDict, JsonWriteOptions);
            _logger.LogInformation("Merged yfinance supplement into latest report for {Symbol} (GrossProfit={GP}, OperatingProfit={OP}, Equity={EQ})",
                symbol, grossProfit, operatingProfit, equity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error merging yfinance supplement for {Symbol}", symbol);
        }
    }

    private static decimal? ReadNullableDecimalFromJson(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
            return d;
        return null;
    }

    private List<NewsSourceConfig> ResolveFinancialSources()
    {
        var configured = _options.Sources
            .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Name))
            .OrderBy(s => s.Priority)
            .ToList();

        if (configured.Count > 0)
        {
            return configured;
        }

        return
        [
            new NewsSourceConfig
            {
                Name = "VietCap-GraphQL",
                Kind = "GraphQlPrimary",
                Priority = 10,
                MaxItems = _options.MaxReportsPerSymbol,
                MinItemsBeforeFallback = _options.MinReportsBeforeFallback,
                FallbackSources =
                [
                    new NewsSourceConfig { Name = "VietStock-HTML", Kind = "HtmlBuiltin", HtmlTemplate = "VietStock", Priority = 20, MaxItems = 5 },
                    new NewsSourceConfig { Name = "CafeF-HTML", Kind = "HtmlBuiltin", HtmlTemplate = "CafeF", Priority = 30, MaxItems = 5 }
                ]
            }
        ];
    }

    private async Task<IReadOnlyList<FinancialReport>> CrawlSourceWithFallbackAsync(
        NewsSourceConfig source,
        string symbol,
        int maxReports,
        int pipelineMinBeforeFallback,
        HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var key = $"{source.Name}|{source.Kind}|{source.Url}|{source.HtmlTemplate}";
        if (!visited.Add(key))
        {
            _logger.LogWarning("Detected recursive financial fallback config for source {SourceName}.", source.Name);
            return Array.Empty<FinancialReport>();
        }

        var sourceMax = Math.Min(maxReports, source.MaxItems ?? maxReports);
        var fetched = (await CrawlSingleFinancialSourceAsync(source, symbol, sourceMax)).ToList();
        var threshold = Math.Max(0, source.MinItemsBeforeFallback ?? pipelineMinBeforeFallback);

        _logger.LogInformation(
            "Financial source {SourceName} fetched={FetchedCount} threshold={Threshold} fallbackCandidates={FallbackCount}",
            source.Name,
            fetched.Count,
            threshold,
            source.FallbackSources.Count);

        if (fetched.Count >= threshold || source.FallbackSources.Count == 0)
        {
            return fetched;
        }

        var aggregate = new List<FinancialReport>(fetched);
        foreach (var fallback in source.FallbackSources.Where(s => s.Enabled).OrderBy(s => s.Priority))
        {
            var nestedVisited = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
            var fallbackItems = await CrawlSourceWithFallbackAsync(
                fallback,
                symbol,
                sourceMax,
                pipelineMinBeforeFallback,
                nestedVisited);
            aggregate.AddRange(fallbackItems);

            if (aggregate.Count >= threshold)
            {
                break;
            }
        }

        return DeduplicateReports(aggregate).Take(sourceMax).ToList();
    }

    private async Task<IEnumerable<FinancialReport>> CrawlSingleFinancialSourceAsync(
        NewsSourceConfig source,
        string symbol,
        int maxReports)
    {
        var kind = source.Kind.Trim();
        if (kind.Equals("GraphQlPrimary", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("VietCap", StringComparison.OrdinalIgnoreCase))
        {
            return await CrawlFromVietCapAsync(symbol, maxReports);
        }

        if (kind.Equals("HtmlBuiltin", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(source.HtmlTemplate, "VietStock", StringComparison.OrdinalIgnoreCase))
            {
                return await CrawlFromVietStockAsync(symbol, maxReports);
            }

            if (string.Equals(source.HtmlTemplate, "CafeF", StringComparison.OrdinalIgnoreCase))
            {
                return await CrawlFromCafeFAsync(symbol, maxReports);
            }
        }

        _logger.LogWarning("Unknown financial source kind {Kind} for {SourceName}.", source.Kind, source.Name);
        return Array.Empty<FinancialReport>();
    }

    private static IEnumerable<FinancialReport> DeduplicateReports(IEnumerable<FinancialReport> reports)
    {
        return reports
            .GroupBy(r => new
            {
                Type = r.ReportType,
                r.Year,
                Quarter = r.Quarter ?? 0,
                Day = r.ReportDate.Date
            })
            .Select(g => g.First())
            .OrderByDescending(r => r.ReportDate);
    }

    /// <summary>
    /// VietCap public GraphQL (same source as vnstock VCI). HTML scrapers often fail because tables load via JS.
    /// </summary>
    private async Task<List<FinancialReport>> CrawlFromVietCapAsync(string symbol, int maxReports)
    {
        var reports = new List<FinancialReport>();
        var upper = symbol.Trim().ToUpperInvariant();
        if (upper.Length == 0 || maxReports <= 0)
            return reports;

        try
        {
            const string query = """
                query Q($ticker: String!, $period: String!) {
                  CompanyFinancialRatio(ticker: $ticker, period: $period) {
                    ratio {
                      ticker
                      yearReport
                      lengthReport
                      updateDate
                      revenue
                      netProfit
                      eps
                      roe
                      roa
                    }
                  }
                }
                """;

            var payload = JsonSerializer.Serialize(new
            {
                query,
                variables = new { ticker = upper, period = "Q" }
            }, JsonWriteOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, VietCapGraphQlUrl);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Origin", "https://trading.vietcap.com.vn/");
            request.Headers.TryAddWithoutValidation("Referer", "https://trading.vietcap.com.vn/");

            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "VietCap financial ratio request failed for {Symbol}: {Status} {Body}",
                    upper,
                    (int)response.StatusCode,
                    body.Length > 500 ? body[..500] : body);
                return reports;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0)
            {
                _logger.LogWarning("VietCap GraphQL returned errors for {Symbol}", upper);
                return reports;
            }

            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("CompanyFinancialRatio", out var cfr) ||
                cfr.ValueKind == JsonValueKind.Null ||
                !cfr.TryGetProperty("ratio", out var ratioArr) ||
                ratioArr.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("VietCap: no ratio array for {Symbol}", upper);
                return reports;
            }

            foreach (var row in ratioArr.EnumerateArray())
            {
                if (reports.Count >= maxReports)
                    break;

                if (!row.TryGetProperty("yearReport", out var yEl) ||
                    yEl.ValueKind != JsonValueKind.Number ||
                    !yEl.TryGetInt32(out var year))
                    continue;

                if (!row.TryGetProperty("lengthReport", out var qEl) ||
                    qEl.ValueKind != JsonValueKind.Number ||
                    !qEl.TryGetInt32(out var lengthReport) ||
                    lengthReport is < 1 or > 4)
                    continue;

                if (!row.TryGetProperty("updateDate", out var uEl) ||
                    uEl.ValueKind != JsonValueKind.Number ||
                    !uEl.TryGetInt64(out var updateMs))
                    continue;

                var revenue = ReadNullableLong(row, "revenue");
                var netProfit = ReadNullableLong(row, "netProfit");
                var eps = ReadNullableDecimal(row, "eps");
                var roe = ReadNullableDecimal(row, "roe");
                var roa = ReadNullableDecimal(row, "roa");

                var content = JsonSerializer.Serialize(
                    new Dictionary<string, object?>
                    {
                        ["Source"] = "VietCap",
                        ["Revenue"] = revenue,
                        ["NetProfit"] = netProfit,
                        ["EPS"] = eps,
                        ["ROE"] = roe,
                        ["ROA"] = roa,
                        ["Year"] = year,
                        ["Quarter"] = lengthReport
                    },
                    JsonWriteOptions);

                var reportDate = DateTimeOffset.FromUnixTimeMilliseconds(updateMs).UtcDateTime;

                reports.Add(new FinancialReport
                {
                    ReportType = "Quarterly",
                    Year = year,
                    Quarter = lengthReport,
                    Content = content,
                    ReportDate = reportDate,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (reports.Count > 0)
                _logger.LogInformation("Crawled {Count} reports from VietCap for {Symbol}", reports.Count, upper);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error crawling VietCap for {Symbol}", symbol);
        }

        return reports;
    }

    private static long? ReadNullableLong(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;
        return el.TryGetInt64(out var v) ? v : null;
    }

    private static decimal? ReadNullableDecimal(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
            return d;
        return null;
    }

    public async Task<IEnumerable<FinancialReport>> CrawlFromVietStockAsync(string symbol, int maxReports = 5)
    {
        var reports = new List<FinancialReport>();

        try
        {
            // Prefer Vietnamese path; legacy financials.htm may redirect.
            var url = $"https://finance.vietstock.vn/{symbol}/tai-chinh.htm";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse financial data tables (often empty in raw HTML — tables load via JS)
            var tables = doc.DocumentNode.SelectNodes("//table[@class='table-financials']")
                ?? doc.DocumentNode.SelectNodes("//table[contains(@class,'table-financial')]");
            if (tables == null || !tables.Any())
            {
                _logger.LogWarning("No financial tables found for {Symbol} on VietStock", symbol);
                return reports;
            }

            // Extract quarterly and annual reports
            var currentYear = DateTime.Now.Year;
            for (int year = currentYear; year >= currentYear - 3 && reports.Count < maxReports; year--)
            {
                // Try to get quarterly reports
                for (int quarter = 4; quarter >= 1 && reports.Count < maxReports; quarter--)
                {
                    var reportData = ExtractFinancialData(tables, year, quarter);
                    if (reportData != null && !string.IsNullOrEmpty(reportData))
                    {
                        reports.Add(new FinancialReport
                        {
                            ReportType = "Quarterly",
                            Year = year,
                            Quarter = quarter,
                            Content = reportData,
                            ReportDate = new DateTime(year, quarter * 3, 1),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Try to get annual report
                var annualData = ExtractFinancialData(tables, year, null);
                if (annualData != null && !string.IsNullOrEmpty(annualData) && reports.Count < maxReports)
                {
                    reports.Add(new FinancialReport
                    {
                        ReportType = "Annual",
                        Year = year,
                        Quarter = null,
                        Content = annualData,
                        ReportDate = new DateTime(year, 12, 31),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _logger.LogInformation("Crawled {Count} reports from VietStock for {Symbol}", reports.Count, symbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling VietStock for {Symbol}", symbol);
        }

        return reports;
    }

    public async Task<IEnumerable<FinancialReport>> CrawlFromCafeFAsync(string symbol, int maxReports = 5)
    {
        var reports = new List<FinancialReport>();

        try
        {
            var cafeFUrlYear = DateTime.UtcNow.Year;
            // Many symbols redirect to generic pages; kept as last-resort fallback.
            var url =
                $"https://s.cafef.vn/bao-cao-tai-chinh/{symbol}/IncSta/{cafeFUrlYear}/0/0/0/bao-cao-ket-qua-hoat-dong-kinh-doanh-cua-cong-ty-co-phan-{symbol}.chn";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse financial statement table
            var table = doc.DocumentNode.SelectSingleNode("//table[@id='tableContent']");
            if (table == null)
            {
                _logger.LogWarning("No financial table found for {Symbol} on CafeF", symbol);
                return reports;
            }

            // Extract data from table
            var headers = table.SelectNodes(".//thead//th");
            var rows = table.SelectNodes(".//tbody//tr");

            if (headers != null && rows != null)
            {
                var financialData = new Dictionary<string, object>();
                
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells != null && cells.Count > 0)
                    {
                        var label = cells[0].InnerText.Trim();
                        var values = new List<string>();
                        
                        for (int i = 1; i < cells.Count && i < headers.Count; i++)
                        {
                            values.Add(cells[i].InnerText.Trim());
                        }
                        
                        financialData[label] = values;
                    }
                }

                // Create reports from extracted data
                var currentYear = DateTime.Now.Year;
                for (int year = currentYear; year >= currentYear - 2 && reports.Count < maxReports; year--)
                {
                    for (int quarter = 4; quarter >= 1 && reports.Count < maxReports; quarter--)
                    {
                        reports.Add(new FinancialReport
                        {
                            ReportType = "Quarterly",
                            Year = year,
                            Quarter = quarter,
                            Content = JsonSerializer.Serialize(financialData),
                            ReportDate = new DateTime(year, quarter * 3, 1),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            _logger.LogInformation("Crawled {Count} reports from CafeF for {Symbol}", reports.Count, symbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling CafeF for {Symbol}", symbol);
        }

        return reports;
    }

    private string? ExtractFinancialData(HtmlNodeCollection tables, int year, int? quarter)
    {
        try
        {
            var financialData = new Dictionary<string, object>
            {
                ["Year"] = year,
                ["Quarter"] = quarter,
                ["Revenue"] = 0,
                ["GrossProfit"] = 0,
                ["OperatingProfit"] = 0,
                ["NetProfit"] = 0,
                ["EPS"] = 0,
                ["TotalAssets"] = 0,
                ["TotalLiabilities"] = 0,
                ["Equity"] = 0,
                ["ROE"] = 0,
                ["ROA"] = 0
            };

            // Parse tables and extract key financial metrics
            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr");
                if (rows == null) continue;

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 2) continue;

                    var label = cells[0].InnerText.Trim();
                    var value = cells[1].InnerText.Trim();

                    // Extract numeric value
                    var numericValue = ExtractNumericValue(value);

                    // Map to financial data fields
                    if (label.Contains("Doanh thu") || label.Contains("Revenue"))
                        financialData["Revenue"] = numericValue;
                    else if (label.Contains("Lợi nhuận gộp") || label.Contains("Gross Profit"))
                        financialData["GrossProfit"] = numericValue;
                    else if (label.Contains("Lợi nhuận hoạt động") || label.Contains("Operating Profit"))
                        financialData["OperatingProfit"] = numericValue;
                    else if (label.Contains("Lợi nhuận sau thuế") || label.Contains("Net Profit"))
                        financialData["NetProfit"] = numericValue;
                    else if (label.Contains("EPS"))
                        financialData["EPS"] = numericValue;
                    else if (label.Contains("Tổng tài sản") || label.Contains("Total Assets"))
                        financialData["TotalAssets"] = numericValue;
                    else if (label.Contains("Nợ phải trả") || label.Contains("Total Liabilities"))
                        financialData["TotalLiabilities"] = numericValue;
                    else if (label.Contains("Vốn chủ sở hữu") || label.Contains("Equity"))
                        financialData["Equity"] = numericValue;
                    else if (label.Contains("ROE"))
                        financialData["ROE"] = numericValue;
                    else if (label.Contains("ROA"))
                        financialData["ROA"] = numericValue;
                }
            }

            return JsonSerializer.Serialize(financialData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting financial data for year {Year} quarter {Quarter}", year, quarter);
            return null;
        }
    }

    private decimal ExtractNumericValue(string text)
    {
        try
        {
            // Remove all non-numeric characters except decimal point and minus sign
            var cleaned = Regex.Replace(text, @"[^\d.-]", "");
            if (decimal.TryParse(cleaned, out var value))
            {
                return value;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return 0;
    }
}

