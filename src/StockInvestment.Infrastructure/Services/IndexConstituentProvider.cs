namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Provider for index constituent symbols (VN30, VN100, HNX30)
/// </summary>
public static class IndexConstituentProvider
{
    /// <summary>
    /// Get symbols for a specific index
    /// </summary>
    public static IReadOnlyList<string> GetSymbols(string indexCode)
    {
        return indexCode.ToUpper() switch
        {
            "VN30" => VN30_SYMBOLS,
            "VN100" => VN100_SYMBOLS,
            "HNX30" => HNX30_SYMBOLS,
            _ => Array.Empty<string>()
        };
    }

    // VN30 symbols - same as StockPriceUpdateJob
    private static readonly string[] VN30_SYMBOLS = new[]
    {
        "VIC", "VNM", "VCB", "VRE", "VHM", "GAS", "MSN", "BID", "CTG", "HPG",
        "TCB", "MBB", "VPB", "PLX", "SAB", "VJC", "GVR", "FPT", "POW", "SSI",
        "MWG", "HDB", "ACB", "TPB", "STB", "PDR", "VIB", "BCM", "KDH", "NVL"
    };

    // VN100 symbols - top 100 stocks, using common ones for demo
    private static readonly string[] VN100_SYMBOLS = new[]
    {
        // Include all VN30
        "VIC", "VNM", "VCB", "VRE", "VHM", "GAS", "MSN", "BID", "CTG", "HPG",
        "TCB", "MBB", "VPB", "PLX", "SAB", "VJC", "GVR", "FPT", "POW", "SSI",
        "MWG", "HDB", "ACB", "TPB", "STB", "PDR", "VIB", "BCM", "KDH", "NVL",
        // Additional VN100 stocks
        "DGC", "DPM", "VCI", "GMD", "DHG", "PNJ", "HSG", "DIG", "VGC", "TCH",
        "HNG", "DXG", "NT2", "PC1", "DCM", "GEX", "VND", "NLG", "VHC", "SBT",
        "VCG", "VSH", "REE", "PVD", "PVT", "PVS", "IDC", "PHR", "BMP", "BMI",
        "VNE", "SCS", "PPC", "DRC", "STK", "HDG", "FLC", "PME", "TLG", "HAG",
        "CMG", "LGC", "DPR", "VPI", "AAA", "PAN", "VCF", "HT1", "SZC", "BWE"
    };

    // HNX30 symbols - top 30 HNX stocks
    private static readonly string[] HNX30_SYMBOLS = new[]
    {
        "PVS", "VCS", "CEO", "NVB", "PVI", "SHB", "TNG", "VCG", "NTP", "PVB",
        "VC3", "ACB", "IDC", "VGC", "THD", "DGC", "SHS", "BVS", "NRC", "DTD",
        "TIG", "PLC", "NDN", "HUT", "MBS", "LAS", "VC2", "AMD", "VIG", "HBS"
    };
}
