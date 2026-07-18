using System.Text;
using System.Text.RegularExpressions;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Best-effort UI hint for an IC identifier embedded in a BIN name or printable header text.</summary>
internal static partial class FirmwareIcIdentifierDetector
{
    private const int HeaderProbeLength = 256 * 1024;

    /// <summary>
    /// Detects an NT519xx marker without treating it as authoritative firmware metadata.
    /// The result is used only to ask the operator whether the shared context should change.
    /// </summary>
    public static string? TryDetect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Match fileNameMatch = IcMarker().Match(Path.GetFileNameWithoutExtension(path));
        if (fileNameMatch.Success)
        {
            return $"NT{fileNameMatch.Groups["ic"].Value}";
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            int length = checked((int)Math.Min(stream.Length, HeaderProbeLength));
            if (length == 0)
            {
                return null;
            }

            byte[] bytes = new byte[length];
            stream.ReadExactly(bytes);
            Match headerMatch = IcMarker().Match(Encoding.ASCII.GetString(bytes));
            return headerMatch.Success ? $"NT{headerMatch.Groups["ic"].Value}" : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Detects an IC marker from a bounded immutable inspection snapshot without file I/O.</summary>
    public static string? TryDetect(string path, WorkbenchFirmwareArtifactSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(snapshot);

        Match fileNameMatch = IcMarker().Match(Path.GetFileNameWithoutExtension(path));
        if (fileNameMatch.Success)
        {
            return $"NT{fileNameMatch.Groups["ic"].Value}";
        }

        Match headerMatch = IcMarker().Match(Encoding.ASCII.GetString(snapshot.GetHeaderProbe().Span));
        return headerMatch.Success ? $"NT{headerMatch.Groups["ic"].Value}" : null;
    }

    [GeneratedRegex(@"(?<!\d)(?:NT)?(?<ic>519\d{2})(?:TT)?(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IcMarker();
}
