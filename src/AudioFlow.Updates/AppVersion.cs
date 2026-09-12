using System.Globalization;

namespace AudioFlow.Updates;

/// <summary>
/// Semantic version (MAJOR.MINOR.PATCH with optional prerelease), tolerant of a
/// leading "v" and build metadata. Prereleases sort before the release.
/// </summary>
public sealed class AppVersion : IComparable<AppVersion>, IEquatable<AppVersion>
{
    public AppVersion(int major, int minor, int patch, string? preRelease = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }

    public bool IsPreRelease => PreRelease is not null;

    public static AppVersion Parse(string text)
    {
        if (!TryParse(text, out var version))
        {
            throw new FormatException($"Invalid semantic version: '{text}'.");
        }

        return version!;
    }

    public static bool TryParse(string? text, out AppVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        string? preRelease = null;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = value[(dash + 1)..];
            value = value[..dash];
        }

        var parts = value.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major))
        {
            return false;
        }

        var minor = 0;
        var patch = 0;
        if (parts.Length > 1 && !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor))
        {
            return false;
        }

        if (parts.Length > 2 && !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out patch))
        {
            return false;
        }

        version = new AppVersion(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        if (PreRelease is null && other.PreRelease is null)
        {
            return 0;
        }

        if (PreRelease is null)
        {
            return 1; // release > prerelease
        }

        if (other.PreRelease is null)
        {
            return -1;
        }

        return string.CompareOrdinal(PreRelease, other.PreRelease);
    }

    public bool Equals(AppVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is AppVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public override string ToString() =>
        PreRelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}
