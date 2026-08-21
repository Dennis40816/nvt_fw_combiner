using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Contracts.VersionManagement;

/// <summary>Canonical Windows-safe relative-file grammar for managed-version identities.</summary>
public static class ManagedRelativePathRules
{
    /// <summary>The maximum admitted relative-path length.</summary>
    public const int MaximumCharacters = 512;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "CONIN$",
        "CONOUT$",
        "CLOCK$",
    };

    /// <summary>Returns whether a slash-separated path is a portable, non-device relative file identity.</summary>
    /// <param name="value">Candidate relative path.</param>
    /// <returns><see langword="true"/> when every segment is safe for managed Windows materialization.</returns>
    public static bool IsSafeFilePath([NotNullWhen(true)] string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <= MaximumCharacters &&
               value[0] != '/' &&
               !value.Contains('\\', StringComparison.Ordinal) &&
               !value.Contains(':', StringComparison.Ordinal) &&
               !value.Any(static character => character is < ' ' or '<' or '>' or '"' or '|' or '?' or '*') &&
               !value.Split('/').Any(static segment =>
                   string.IsNullOrWhiteSpace(segment) ||
                   segment is "." or ".." ||
                   segment.EndsWith(' ') ||
                   segment.EndsWith('.') ||
                   IsReservedDeviceName(segment));
    }

    private static bool IsReservedDeviceName(string segment)
    {
        string stem = segment.Split('.', 2)[0];
        return ReservedDeviceNames.Contains(stem) ||
               (stem.Length == 4 &&
                stem[3] is >= '1' and <= '9' &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)));
    }
}
