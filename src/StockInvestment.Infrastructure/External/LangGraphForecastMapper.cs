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

    private const int KeyDriverMaxChars = 360;

    private static string TruncateDriver(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length <= KeyDriverMaxChars)
            return t;
        return string.Concat(t.AsSpan(0, KeyDriverMaxChars - 1), "…");
    }

    private static void AddDriverIfRoom(List<string> list, string line)
    {
        if (list.Count >= 5 || string.IsNullOrWhiteSpace(line))
            return;
        var normalized = line.Trim();
        foreach (var existing in list)
        {
            if (string.Equals(existing, normalized, StringComparison.Ordinal))
                return;
        }

        list.Add(normalized);
    }

    private static List<string> BuildKeyDrivers(LangGraphAnalyzeResponse r)
    {
        var list = new List<string>();
        var news = r.NewsEvidence ?? new List<LangGraphNewsEvidenceItem>();
        foreach (var item in news.Take(5))
        {
            var title = (item.Title ?? "").Trim();
            var snippet = (item.Snippet ?? "").Trim();
            var why = (item.WhyItMatters ?? "").Trim();

            if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(snippet))
            {
                var line = string.IsNullOrEmpty(snippet) ? title : $"{title}: {snippet}";
                AddDriverIfRoom(list, TruncateDriver(line));
                continue;
            }

            if (!string.IsNullOrEmpty(why))
            {
                var sentiment = (item.Sentiment ?? "").Trim();
                var line = string.IsNullOrEmpty(sentiment)
                    ? why
                    : $"{sentiment}: {why}";
                AddDriverIfRoom(list, TruncateDriver(line));
            }
        }

        var ds = r.DebateSummary;
        if (ds != null)
        {
            AddDriverIfRoom(list, string.IsNullOrWhiteSpace(ds.FinalDecision)
                ? ""
                : $"Quyết định tổng hợp: {TruncateDriver(ds.FinalDecision)}");
            AddDriverIfRoom(list, string.IsNullOrWhiteSpace(ds.NewsAgent)
                ? ""
                : $"Tóm tắt góc tin: {TruncateDriver(ds.NewsAgent)}");
            AddDriverIfRoom(list, string.IsNullOrWhiteSpace(ds.TechAgent)
                ? ""
                : $"Tóm tắt góc kỹ thuật: {TruncateDriver(ds.TechAgent)}");
        }

        var tech = r.TechEvidence;
        if (tech != null && list.Count < 5 &&
            (tech.ChangePct.HasValue || tech.Rsi.HasValue || tech.LastClose.HasValue))
        {
            var parts = new List<string>();
            if (tech.ChangePct.HasValue)
                parts.Add(FormattableString.Invariant($"biến động {tech.ChangePct:F2}%"));
            if (tech.Rsi.HasValue)
                parts.Add(FormattableString.Invariant($"RSI {tech.Rsi:F1}"));
            if (tech.LastClose.HasValue)
                parts.Add(FormattableString.Invariant($"giá đóng cửa gần nhất {tech.LastClose:F2}"));
            if (parts.Count > 0)
                AddDriverIfRoom(list, "Chỉ báo kỹ thuật: " + string.Join(", ", parts));
        }

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(r.Reasoning))
            AddDriverIfRoom(list, "Luận điểm: " + TruncateDriver(r.Reasoning!));

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
