namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Formats local firmware paths consistently for display without changing the selected artifact path.</summary>
internal static class FirmwarePathDisplay
{
    internal static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Path.GetFullPath(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }
}
