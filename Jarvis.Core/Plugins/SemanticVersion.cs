using System.Globalization;
using System.Text.RegularExpressions;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Lightweight Semantic Version 2.0.0 implementation used for plugin version management.
/// </summary>
public readonly struct SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private static readonly Regex Pattern = new(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+(?<build>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public SemanticVersion(int major, int minor, int patch, string? preRelease = null)
    {
        if (major < 0 || minor < 0 || patch < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), "Version components cannot be negative.");
        }

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>Pre-release label (e.g. "beta.1"), or null for a stable release.</summary>
    public string? PreRelease { get; }

    public static bool TryParse(string? input, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        Match match = Pattern.Match(input);
        if (!match.Success)
        {
            return false;
        }

        version = new SemanticVersion(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null);
        return true;
    }

    public override string ToString()
        => PreRelease is null
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    public int CompareTo(SemanticVersion other)
    {
        int comparison = Major.CompareTo(other.Major);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0)
        {
            return comparison;
        }

        // A stable release (null pre-release) is greater than any pre-release.
        return (PreRelease, other.PreRelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => ComparePreRelease(PreRelease!, other.PreRelease!),
        };
    }

    private static int ComparePreRelease(string left, string right)
    {
        string[] leftParts = left.Split('.');
        string[] rightParts = right.Split('.');
        int length = Math.Min(leftParts.Length, rightParts.Length);

        for (int i = 0; i < length; i++)
        {
            int comparison = ComparePreReleasePart(leftParts[i], rightParts[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static int ComparePreReleasePart(string left, string right)
    {
        bool leftIsNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out int leftNumber);
        bool rightIsNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out int rightNumber);

        if (leftIsNumeric && rightIsNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftIsNumeric)
        {
            return -1; // numeric identifiers have lower precedence than alphanumeric
        }

        if (rightIsNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }

    public bool Equals(SemanticVersion other)
        => Major == other.Major
           && Minor == other.Minor
           && Patch == other.Patch
           && string.Equals(PreRelease, other.PreRelease, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public static bool operator ==(SemanticVersion left, SemanticVersion right) => left.Equals(right);

    public static bool operator !=(SemanticVersion left, SemanticVersion right) => !left.Equals(right);
}
