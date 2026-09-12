using System.Security.Cryptography;

namespace AudioFlow.Updates;

/// <summary>SHA-256 helpers for verifying downloaded packages.</summary>
public static class Checksum
{
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    /// <summary>Verifies a file against an expected hex SHA-256 (case-insensitive).</summary>
    public static bool Verify(string filePath, string expectedHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex) || !File.Exists(filePath))
        {
            return false;
        }

        var normalized = expectedHex.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        var actual = ComputeSha256(filePath);
        return string.Equals(actual, normalized, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Finds the hash for a file name inside a SHA256SUMS file.</summary>
    public static string? FindInSumsFile(string sumsContent, string fileName)
    {
        foreach (var rawLine in sumsContent.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var name = parts[^1].TrimStart('*').Trim();
            if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0];
            }
        }

        return null;
    }
}
