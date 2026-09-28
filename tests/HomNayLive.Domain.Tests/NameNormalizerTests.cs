using HomNayLive.Domain.Normalization;

namespace HomNayLive.Domain.Tests;

public class NameNormalizerTests
{
    [Fact]
    public void Normalize_TrimsAndCollapsesWhitespace_AndLowercasesInvariantly()
    {
        var value = "  Môi Trường   Hòa Bình  ";

        var result = NameNormalizer.Normalize(value);

        Assert.Equal("môi trường hòa bình", result);
    }

    [Fact]
    public void Normalize_PreservesDiacriticsAndNfcNormalization()
    {
        var value = "Cafe\u0301";

        var result = NameNormalizer.Normalize(value);

        Assert.Equal("café", result);
    }
}
