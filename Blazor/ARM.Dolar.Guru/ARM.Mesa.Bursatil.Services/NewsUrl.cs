using System.Net;

namespace ARM.Mesa.Bursatil.Services;

public static class NewsUrl
{
    public static string? Canonicalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort ||
            uri.HostNameType != UriHostNameType.Dns || !uri.Host.Contains('.') ||
            uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(uri.Host, out _)) return null;

        var builder = new UriBuilder(uri) { Fragment = "", Host = uri.IdnHost.ToLowerInvariant() };
        // Keep semantic query parameters (including article IDs); drop only known trackers.
        builder.Query = string.Join("&", uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !IsTrackingParameter(Uri.UnescapeDataString(part.Split('=')[0])))
            .Order(StringComparer.Ordinal));
        return builder.Uri.AbsoluteUri;
    }

    private static bool IsTrackingParameter(string name) =>
        name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
        new[] { "fbclid", "gclid", "dclid", "msclkid", "mc_cid", "mc_eid", "_ga", "_gl" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);
}
