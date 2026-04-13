namespace StockInvestment.Domain.Constants;

/// <summary>
/// Official VN30 constituent universe for this app (reduces outbound market-data calls).
/// </summary>
public static class Vn30Universe
{
    private static readonly string[] SymbolsOrdered =
    {
        "VIC", "VNM", "VCB", "VRE", "VHM", "GAS", "MSN", "BID", "CTG", "HPG",
        "TCB", "MBB", "VPB", "PLX", "SAB", "VJC", "GVR", "FPT", "POW", "SSI",
        "MWG", "HDB", "ACB", "TPB", "STB", "PDR", "VIB", "BCM", "KDH", "NVL"
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
