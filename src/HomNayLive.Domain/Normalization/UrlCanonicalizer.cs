using System.Globalization;
using System.Text;

namespace HomNayLive.Domain.Normalization;

public static class UrlCanonicalizer
{
    private static readonly UrlCanonicalizationOptions DefaultOptions = new();

    public static string Canonicalize(string url, UrlCanonicalizationOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("URL must be absolute.", nameof(url));
        }

        var opt = options ?? DefaultOptions;
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.IdnHost.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Fragment = string.Empty
        };

        var queryParams = new SortedDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var query = uri.Query.TrimStart('?');
            foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = segment.Split('=', 2);
                var key = Uri.UnescapeDataString(pair[0]);
                if (opt.TrackingParameterNames.Contains(key))
                {
                    continue;
                }

                var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
                queryParams[key] = value;
            }
        }

        var path = uri.AbsolutePath;
        path = System.Text.RegularExpressions.Regex.Replace(path, @"/+", "/");
        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? string.Empty)}"));
        var canonical = new UriBuilder(builder.Uri)
        {
            Path = path,
            Query = queryString
        }.Uri;

        var result = canonical.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.UriEscaped);
        return result.TrimEnd('/');
    }
}
