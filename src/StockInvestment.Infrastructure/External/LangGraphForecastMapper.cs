using System.Globalization;
using System.Text;
using StockInvestment.Application.DTOs.LangGraph;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.External;

public sealed class LangGraphForecastMapper : ILangGraphForecastMapper
{
    public ForecastResult Map(LangGraphAnalyzeResponse response, string symbol, string timeHorizon)
    {
        ArgumentNullException.ThrowIfNull(response);

        var normalizedHorizon = string.IsNullOrWhiteSpace(timeHorizon) ? "short" : timeHorizon.Trim().ToLowerInvariant();
        var sym = string.IsNullOrWhiteSpace(symbol) ? (response.Symbol ?? "").Trim().ToUpperInvariant() : symbol.Trim().ToUpperInvariant();

        var trend = MapTrend(response.Forecast);
        var score = (int)Math.Clamp(Math.Round(response.Confidence ?? 50.0), 0, 100);
        var confidenceLabel = score >= 70 ? "High" : score >= 40 ? "Medium" : "Low";
        var recommendation = trend switch
        {
            "Up" => "Buy",
            "Down" => "Sell",
            _ => "Hold"
        };

        var keyDrivers = BuildKeyDrivers(response);
        var risks = BuildRisks(response);
        var analysis = BuildAnalysis(response, normalizedHorizon);

        return new ForecastResult
        {
            Symbol = sym,
            Trend = trend,
            Confidence = confidenceLabel,
            ConfidenceScore = score,
            TimeHorizon = normalizedHorizon,
            Recommendation = recommendation,
            KeyDrivers = keyDrivers,
            Risks = risks,
            Analysis = analysis,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static string MapTrend(string? forecast)
    {
        var f = (forecast ?? "").Trim().ToUpperInvariant();
        if (f is "UP") return "Up";
        if (f is "DOWN" or "DOWN_SLIGHTLY") return "Down";
        return "Sideways";
    }

    private static List<string> BuildKeyDrivers(LangGraphAnalyzeResponse r)
    {
        var list = new List<string>();
        var news = r.NewsEvidence ?? new List<LangGraphNewsEvidenceItem>();
        foreach (var item in news.Take(5))
        {
            var title = (item.Title ?? "").Trim();
            var snippet = (item.Snippet ?? "").Trim();
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(snippet))
                continue;
            var line = string.IsNullOrEmpty(snippet) ? title : $"{title}: {snippet}";
            list.Add(line.Trim());
        }

        var ds = r.DebateSummary;
        if (ds != null && list.Count < 5 && !string.IsNullOrWhiteSpace(ds.FinalDecision))
            list.Add($"Quyết định tổng hợp: {ds.FinalDecision.Trim()}");

        return list;
    }

    private static List<string> BuildRisks(LangGraphAnalyzeResponse r)
    {
        var list = new List<string>();
        foreach (var rc in r.RiskConditions ?? new List<LangGraphRiskConditionItem>())
        {
            var t = (rc.Trigger ?? "").Trim();
            var w = (rc.WhatToWatch ?? "").Trim();
            if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(w))
                continue;
            list.Add(string.IsNullOrEmpty(w) ? t : $"{t} — Theo dõi: {w}");
        }

        return list;
    }

    private static string BuildAnalysis(LangGraphAnalyzeResponse r, string timeHorizon)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"[LangGraph] Khung thời gian UI: {timeHorizon} (một lần chạy graph; không tách short/medium/long.)");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(r.Reasoning))
        {
            sb.AppendLine(r.Reasoning.Trim());
            sb.AppendLine();
        }

        var ds = r.DebateSummary;
        if (ds != null)
        {
            if (!string.IsNullOrWhiteSpace(ds.NewsAgent))
                sb.AppendLine("Tóm tắt góc tin:").AppendLine(ds.NewsAgent.Trim()).AppendLine();
            if (!string.IsNullOrWhiteSpace(ds.TechAgent))
                sb.AppendLine("Tóm tắt góc kỹ thuật:").AppendLine(ds.TechAgent.Trim()).AppendLine();
            if (!string.IsNullOrWhiteSpace(ds.FinalDecision))
                sb.AppendLine("Quyết định cuối:").AppendLine(ds.FinalDecision.Trim());
        }

        var tech = r.TechEvidence;
        if (tech != null && (tech.LastClose.HasValue || tech.ChangePct.HasValue))
        {
            sb.AppendLine();
            sb.AppendLine("Tech evidence (snapshot):");
            if (tech.FirstClose.HasValue) sb.AppendLine(CultureInfo.InvariantCulture, $"  first_close: {tech.FirstClose}");
            if (tech.LastClose.HasValue) sb.AppendLine(CultureInfo.InvariantCulture, $"  last_close: {tech.LastClose}");
            if (tech.ChangePct.HasValue) sb.AppendLine(CultureInfo.InvariantCulture, $"  change_pct: {tech.ChangePct}");
            if (tech.Rsi.HasValue) sb.AppendLine(CultureInfo.InvariantCulture, $"  rsi: {tech.Rsi}");
        }

        return sb.ToString().Trim();
    }
}
