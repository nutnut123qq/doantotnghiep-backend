namespace StockInvestment.Infrastructure.Configuration;

/// <summary>
/// Drops news whose article URL path contains a blocked segment (e.g. phap-luat).
/// </summary>
public static class NewsUrlPathFilter
{
    /// <summary>
    /// Returns true when the URL should be kept. Empty URL or unparseable URL is rejected.
    /// When <paramref name="blockedSegments"/> is null or empty, any non-empty absolute URL is allowed.
    /// </summary>
    public static bool IsAllowed(string? url, IEnumerable<string>? blockedSegments)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (blockedSegments == null)
            return true;

        var blocked = blockedSegments
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (blocked.Count == 0)
            return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var seg in segments)
        {
            if (blocked.Contains(seg))
                return false;
        }

        return true;
    }
}
