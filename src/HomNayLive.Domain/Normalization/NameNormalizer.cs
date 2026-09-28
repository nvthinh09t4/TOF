using System.Globalization;
using System.Text;

namespace HomNayLive.Domain.Normalization;

public static class NameNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var collapsed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ");
        var normalized = collapsed.Normalize(NormalizationForm.FormC);
        return normalized.ToLowerInvariant();
    }
}
