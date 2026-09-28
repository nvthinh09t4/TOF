using HomNayLive.Domain.Normalization;

namespace HomNayLive.Domain.Tests;

public class UrlCanonicalizerTests
{
    [Fact]
    public void Canonicalize_StripsTrackingParams_AndSortsQuery_AndLowercasesHost()
    {
        var url = "HTTPS://Example.com:443/path?b=2&utm_source=x&a=1&fbclid=123&x=1";

        var result = UrlCanonicalizer.Canonicalize(url);

        Assert.Equal("https://example.com/path?a=1&b=2&x=1", result);
    }

    [Fact]
    public void Canonicalize_DropsFragment_AndTrailingSlash_AndCollapsesDuplicateSlashes()
    {
        var url = "https://example.com//a//b/#section";

        var result = UrlCanonicalizer.Canonicalize(url);

        Assert.Equal("https://example.com/a/b", result);
    }
}
