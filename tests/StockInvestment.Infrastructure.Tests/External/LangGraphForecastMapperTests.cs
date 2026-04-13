using StockInvestment.Application.DTOs.LangGraph;
using StockInvestment.Infrastructure.External;
using Xunit;

namespace StockInvestment.Infrastructure.Tests.External;

public class LangGraphForecastMapperTests
{
    private readonly LangGraphForecastMapper _mapper = new();

    [Fact]
    public void Map_UP_maps_to_Up_Buy_and_high_confidence_band()
    {
        var dto = new LangGraphAnalyzeResponse
        {
            Forecast = "UP",
            Confidence = 85,
            Reasoning = "Test reasoning.",
            DebateSummary = new LangGraphDebateSummary
            {
                FinalDecision = "Bias up."
            },
            NewsEvidence = new List<LangGraphNewsEvidenceItem>
            {
                new() { Title = "Tin A", Snippet = "Chi tiết" }
            },
            RiskConditions = new List<LangGraphRiskConditionItem>
            {
                new() { Trigger = "Giảm sàn", WhatToWatch = "Thanh khoản" }
            }
        };

        var r = _mapper.Map(dto, "VNM", "short");

        Assert.Equal("VNM", r.Symbol);
        Assert.Equal("Up", r.Trend);
        Assert.Equal("Buy", r.Recommendation);
        Assert.Equal("High", r.Confidence);
        Assert.Equal(85, r.ConfidenceScore);
        Assert.Equal("short", r.TimeHorizon);
        Assert.Contains("Tin A", r.KeyDrivers[0]);
        Assert.Contains("Giảm sàn", r.Risks[0]);
        Assert.Contains("LangGraph", r.Analysis);
        Assert.Contains("Test reasoning.", r.Analysis);
    }

    [Fact]
    public void Map_DOWN_SLIGHTLY_maps_to_Down_Sell()
    {
        var dto = new LangGraphAnalyzeResponse
        {
            Forecast = "DOWN_SLIGHTLY",
            Confidence = 35.0,
            Reasoning = "R"
        };

        var r = _mapper.Map(dto, "FPT", "medium");

        Assert.Equal("Down", r.Trend);
        Assert.Equal("Sell", r.Recommendation);
        Assert.Equal("Low", r.Confidence);
        Assert.Equal(35, r.ConfidenceScore);
    }

    [Fact]
    public void Map_SIDEWAYS_maps_to_Hold()
    {
        var dto = new LangGraphAnalyzeResponse
        {
            Forecast = "SIDEWAYS",
            Confidence = 50
        };

        var r = _mapper.Map(dto, "VIC", "long");

        Assert.Equal("Sideways", r.Trend);
        Assert.Equal("Hold", r.Recommendation);
        Assert.Equal("Medium", r.Confidence);
    }
}
