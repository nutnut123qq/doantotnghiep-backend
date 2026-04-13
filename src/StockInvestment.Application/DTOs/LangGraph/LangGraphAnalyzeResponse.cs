using System.Text.Json.Serialization;

namespace StockInvestment.Application.DTOs.LangGraph;

/// <summary>
/// Subset of Python LangGraph <c>graph.invoke</c> state / Risk Judge output (camelCase/snake_case tolerant via case-insensitive JSON).
/// </summary>
public sealed class LangGraphAnalyzeResponse
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("forecast")]
    public string? Forecast { get; set; }

    /// <summary>1–100 from Risk Judge (may deserialize as fractional JSON number).</summary>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    [JsonPropertyName("debate_summary")]
    public LangGraphDebateSummary? DebateSummary { get; set; }

    [JsonPropertyName("news_evidence")]
    public List<LangGraphNewsEvidenceItem>? NewsEvidence { get; set; }

    [JsonPropertyName("risk_conditions")]
    public List<LangGraphRiskConditionItem>? RiskConditions { get; set; }

    [JsonPropertyName("tech_evidence")]
    public LangGraphTechEvidence? TechEvidence { get; set; }
}

public sealed class LangGraphDebateSummary
{
    [JsonPropertyName("news_agent")]
    public string? NewsAgent { get; set; }

    [JsonPropertyName("tech_agent")]
    public string? TechAgent { get; set; }

    [JsonPropertyName("final_decision")]
    public string? FinalDecision { get; set; }
}

public sealed class LangGraphNewsEvidenceItem
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    [JsonPropertyName("sentiment")]
    public string? Sentiment { get; set; }

    [JsonPropertyName("why_it_matters")]
    public string? WhyItMatters { get; set; }
}

public sealed class LangGraphRiskConditionItem
{
    [JsonPropertyName("trigger")]
    public string? Trigger { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("what_to_watch")]
    public string? WhatToWatch { get; set; }

    [JsonPropertyName("mitigation_hint")]
    public string? MitigationHint { get; set; }
}

public sealed class LangGraphTechEvidence
{
    [JsonPropertyName("first_close")]
    public double? FirstClose { get; set; }

    [JsonPropertyName("last_close")]
    public double? LastClose { get; set; }

    [JsonPropertyName("change_pct")]
    public double? ChangePct { get; set; }

    [JsonPropertyName("period_high")]
    public double? PeriodHigh { get; set; }

    [JsonPropertyName("period_low")]
    public double? PeriodLow { get; set; }

    [JsonPropertyName("rsi")]
    public double? Rsi { get; set; }
}
