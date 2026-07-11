namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Immutable in-memory copy of the base flash BIN selected for one General Replace or Hex Editor session.
/// </summary>
public sealed class WorkbenchGeneralReplaceBaseSnapshot
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private readonly byte[] _bytes;

    internal WorkbenchGeneralReplaceBaseSnapshot(string sourcePath, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(bytes);

        SourcePath = Path.GetFullPath(sourcePath);
        _bytes = [.. bytes];
        ArtifactId = VirtualArtifactLocator.CreateGeneralReplaceBaseSnapshot();
    }

    /// <summary>Normalized source path selected by the user when this snapshot was created.</summary>
    public string SourcePath { get; }

    /// <summary>Immutable snapshot length in bytes.</summary>
    public long Length => _bytes.LongLength;

    internal string ArtifactId { get; }

    internal bool IsForSourcePath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
        string.Equals(SourcePath, Path.GetFullPath(path), PathComparison);
    }

    internal ReadOnlySpan<byte> AsSpan()
    {
        return _bytes;
    }

    internal byte[] CopyForArtifactReader()
    {
        return [.. _bytes];
    }
}
