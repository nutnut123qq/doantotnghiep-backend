using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StockInvestment.Infrastructure.External;

public class FinancialReportCrawlerService : IFinancialReportCrawlerService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FinancialReportCrawlerService> _logger;
    private readonly HttpClient _httpClient;

    public FinancialReportCrawlerService(
        IHttpClientFactory httpClientFactory,
        ILogger<FinancialReportCrawlerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("FinancialReportCrawler");
    }

    public async Task<IEnumerable<FinancialReport>> CrawlReportsBySymbolAsync(string symbol, int maxReports = 10)
    {
        var reports = new List<FinancialReport>();

        try
        {
            var vietStockReports = await CrawlFromVietStockAsync(symbol, maxReports / 2);
            reports.AddRange(vietStockReports);

            var cafeFReports = await CrawlFromCafeFAsync(symbol, maxReports / 2);
            reports.AddRange(cafeFReports);

            _logger.LogInformation("Crawled {Count} financial reports for {Symbol}", reports.Count, symbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling financial reports for {Symbol}", symbol);
        }

        return reports.Take(maxReports);
    }

    public async Task<IEnumerable<FinancialReport>> CrawlFromVietStockAsync(string symbol, int maxReports = 5)
    {
        var reports = new List<FinancialReport>();

        try
        {
            // VietStock financial report URL
            var url = $"https://finance.vietstock.vn/{symbol}/financials.htm";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse financial data tables
            var tables = doc.DocumentNode.SelectNodes("//table[@class='table-financials']");
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
            // CafeF financial report URL
            var url = $"https://s.cafef.vn/bao-cao-tai-chinh/{symbol}/IncSta/2024/0/0/0/bao-cao-ket-qua-hoat-dong-kinh-doanh-cua-cong-ty-co-phan-{symbol}.chn";
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

