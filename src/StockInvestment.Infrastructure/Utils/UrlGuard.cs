using System.Net;
using System.Text.RegularExpressions;

namespace StockInvestment.Infrastructure.Utils;

/// <summary>
/// P0-2: SSRF protection utility to validate URLs and prevent Server-Side Request Forgery attacks
/// </summary>
public static class UrlGuard
{
    // Private IP ranges (RFC 1918)
    private static readonly IPNetwork[] PrivateNetworks = new[]
    {
        IPNetwork.Parse("10.0.0.0/8"),        // 10.0.0.0 - 10.255.255.255
        IPNetwork.Parse("172.16.0.0/12"),     // 172.16.0.0 - 172.31.255.255
        IPNetwork.Parse("192.168.0.0/16"),    // 192.168.0.0 - 192.168.255.255
        IPNetwork.Parse("127.0.0.0/8"),       // 127.0.0.0 - 127.255.255.255 (localhost)
        IPNetwork.Parse("169.254.0.0/16"),     // Link-local (169.254.0.0 - 169.254.255.255)
        IPNetwork.Parse("::1/128"),           // IPv6 localhost
        IPNetwork.Parse("fc00::/7"),          // IPv6 private range
        IPNetwork.Parse("fe80::/10")          // IPv6 link-local
    };

    // Cloud metadata service endpoints (common attack targets)
    private static readonly string[] BlockedHosts = new[]
    {
        "metadata.google.internal",
        "169.254.169.254", // AWS/GCP/Azure metadata
        "metadata.azure.com",
        "metadata.azure.net",
        "instance-data",
        "169.254.169.254"
    };

    /// <summary>
    /// Validates a URL to prevent SSRF attacks
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <param name="allowedSchemes">Allowed URL schemes (default: http, https)</param>
    /// <param name="maxRedirects">Maximum number of redirects allowed (default: 5)</param>
    /// <param name="maxResponseSize">Maximum response size in bytes (default: 10MB)</param>
    /// <returns>Validation result with error message if invalid</returns>
    public static UrlValidationResult ValidateUrl(
        string url,
        string[]? allowedSchemes = null,
        int maxRedirects = 5,
        long maxResponseSize = 10 * 1024 * 1024)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return UrlValidationResult.Invalid("URL cannot be null or empty");
        }

        allowedSchemes ??= new[] { "http", "https" };

        // Parse URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return UrlValidationResult.Invalid($"Invalid URL format: {url}");
        }

        // Check scheme
        if (!allowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return UrlValidationResult.Invalid(
                $"URL scheme '{uri.Scheme}' is not allowed. Allowed schemes: {string.Join(", ", allowedSchemes)}");
        }

        // Check for blocked hosts (metadata services)
        var host = uri.Host.ToLowerInvariant();
        if (BlockedHosts.Any(blocked => host.Contains(blocked, StringComparison.OrdinalIgnoreCase)))
        {
            return UrlValidationResult.Invalid(
                $"URL host '{host}' is blocked for security reasons (metadata service protection)");
        }

        // Check for localhost variations
        if (IsLocalhost(host))
        {
            return UrlValidationResult.Invalid(
                $"URL host '{host}' is blocked. Localhost and loopback addresses are not allowed for security reasons.");
        }

        // Resolve and check IP address
        try
        {
            var hostEntry = Dns.GetHostEntry(host);
            foreach (var ip in hostEntry.AddressList)
            {
                // Check if IP is in private range
                if (IsPrivateIp(ip))
                {
                    return UrlValidationResult.Invalid(
                        $"URL resolves to private IP address '{ip}'. Private IPs are not allowed for security reasons.");
                }

                // Check if IP is loopback
                if (IPAddress.IsLoopback(ip))
                {
                    return UrlValidationResult.Invalid(
                        $"URL resolves to loopback address '{ip}'. Loopback addresses are not allowed.");
                }
            }
        }
        catch (Exception ex)
        {
            // DNS resolution failure - log but don't block (could be transient)
            // In production, you might want to be more strict
            return UrlValidationResult.Invalid($"Failed to resolve host '{host}': {ex.Message}");
        }

        return UrlValidationResult.Valid();
    }

    private static bool IsLocalhost(string host)
    {
        var localhostPatterns = new[]
        {
            "localhost",
            "127.0.0.1",
            "::1",
            "0.0.0.0",
            "0:0:0:0:0:0:0:1"
        };

        return localhostPatterns.Any(pattern => 
            host.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith(pattern + ".", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        return PrivateNetworks.Any(network => network.Contains(ip));
    }
}

/// <summary>
/// Result of URL validation
/// </summary>
public class UrlValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }

    private UrlValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static UrlValidationResult Valid() => new(true);
    public static UrlValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Simple IP network representation for CIDR checking
/// </summary>
internal class IPNetwork
{
    public IPAddress Network { get; }
    public int PrefixLength { get; }

    private IPNetwork(IPAddress network, int prefixLength)
    {
        Network = network;
        PrefixLength = prefixLength;
    }

    public static IPNetwork Parse(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var network)
            || !int.TryParse(parts[1], out var prefixLength))
        {
            throw new ArgumentException($"Invalid CIDR notation: {cidr}", nameof(cidr));
        }

        return new IPNetwork(network, prefixLength);
    }

    public bool Contains(IPAddress address)
    {
        if (Network.AddressFamily != address.AddressFamily)
            return false;

        var networkBytes = Network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();

        if (networkBytes.Length != addressBytes.Length)
            return false;

        var bytesToCheck = PrefixLength / 8;
        var bitsToCheck = PrefixLength % 8;

        // Check full bytes
        for (int i = 0; i < bytesToCheck; i++)
        {
            if (networkBytes[i] != addressBytes[i])
                return false;
        }

        // Check partial byte if needed
        if (bitsToCheck > 0 && bytesToCheck < networkBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - bitsToCheck));
            if ((networkBytes[bytesToCheck] & mask) != (addressBytes[bytesToCheck] & mask))
                return false;
        }

        return true;
    }
}
