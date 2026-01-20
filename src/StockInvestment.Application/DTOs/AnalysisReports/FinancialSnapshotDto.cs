namespace StockInvestment.Application.DTOs.AnalysisReports;

public class FinancialSnapshotDto
{
    public string Symbol { get; set; } = string.Empty;
    public string? Period { get; set; }
    public DateTime? ReportDate { get; set; }
    public decimal? Revenue { get; set; }
    public decimal? NetProfit { get; set; }
    public decimal? Eps { get; set; }
    public decimal? Pe { get; set; }
    public decimal? Roe { get; set; }
    public decimal? DebtToEquity { get; set; }
    public string? Notes { get; set; }
    public string? SourceUrl { get; set; }
    public string? RawText { get; set; }
}
