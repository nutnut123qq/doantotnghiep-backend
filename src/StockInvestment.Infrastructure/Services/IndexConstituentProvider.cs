using StockInvestment.Domain.Constants;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Index constituents: only VN30 is supported (VN100/HNX30 removed to limit VNstock usage).
/// </summary>
public static class IndexConstituentProvider
{
    /// <summary>
    /// Returns symbols for the index code, or empty if unsupported.
    /// </summary>
    public static IReadOnlyList<string> GetSymbols(string indexCode)
    {
        return indexCode.ToUpperInvariant() switch
        {
            "VN30" => Vn30Universe.Symbols,
            _ => Array.Empty<string>()
        };
    }
}
