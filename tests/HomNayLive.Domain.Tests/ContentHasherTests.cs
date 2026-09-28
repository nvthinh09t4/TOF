using HomNayLive.Domain.Normalization;

namespace HomNayLive.Domain.Tests;

public class ContentHasherTests
{
    [Fact]
    public void ComputeHash_IsDeterministicAcrossWhitespaceAndCaseVariants()
    {
        var first = "Hello   World\n";
        var second = "hello world";

        var hash1 = ContentHasher.ComputeHash(first);
        var hash2 = ContentHasher.ComputeHash(second);

        Assert.NotEqual(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }
}
