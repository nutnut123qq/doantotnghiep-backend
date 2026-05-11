namespace StockInvestment.Domain.Constants;

/// <summary>
/// Official VN30 constituent universe for this app (reduces outbound market-data calls).
/// </summary>
public static class Vn30Universe
{
    private static readonly string[] SymbolsOrdered =
    {
        "ACB", "BID", "CTG", "DGC", "FPT", "GAS", "GVR", "HDB", "HPG", "LPB",
        "MBB", "MSN", "MWG", "PLX", "SAB", "SHB", "SSB", "SSI", "STB", "TCB",
        "TPB", "VCB", "VHM", "VIB", "VIC", "VJC", "VNM", "VPB", "VPL", "VRE"
    };

    private static readonly HashSet<string> SymbolSet = new(SymbolsOrdered, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Symbols { get; } = Array.AsReadOnly(SymbolsOrdered);

    public static bool Contains(string symbol)
    {
        return !string.IsNullOrWhiteSpace(symbol) && SymbolSet.Contains(symbol.Trim());
    }

    public static string NormalizeOrEmpty(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;
        var t = symbol.Trim().ToUpperInvariant();
        return SymbolSet.Contains(t) ? t : string.Empty;
    }
}
