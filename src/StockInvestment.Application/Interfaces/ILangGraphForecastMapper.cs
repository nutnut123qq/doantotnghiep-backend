using StockInvestment.Application.DTOs.LangGraph;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Maps LangGraph <see cref="LangGraphAnalyzeResponse"/> to dashboard <see cref="ForecastResult"/>.
/// </summary>
public interface ILangGraphForecastMapper
{
    ForecastResult Map(LangGraphAnalyzeResponse response, string symbol, string timeHorizon);
}
