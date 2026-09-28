using System.Security.Cryptography;
using System.Text;

namespace HomNayLive.Domain.Normalization;

public static class ContentHasher
{
    public static string ComputeHash(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var normalized = content.Normalize(NormalizationForm.FormC);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
