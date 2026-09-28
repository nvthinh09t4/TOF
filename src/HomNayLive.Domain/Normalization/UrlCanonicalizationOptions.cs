namespace HomNayLive.Domain.Normalization;

public sealed class UrlCanonicalizationOptions
{
    public HashSet<string> TrackingParameterNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source",
        "utm_medium",
        "utm_campaign",
        "utm_term",
        "utm_content",
        "fbclid",
        "gclid",
        "mc_cid",
        "mc_eid"
    };
}
