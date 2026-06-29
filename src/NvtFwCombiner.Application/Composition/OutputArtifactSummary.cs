namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe summary of composition output.</summary>
public sealed class OutputArtifactSummary
{
    /// <summary>Creates an output summary.</summary>
    public OutputArtifactSummary(string fileName, long size, string sha256, bool committed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        FileName = fileName;
        Size = size;
        Sha256 = sha256;
        Committed = committed;
    }

    /// <summary>Output file name, without a host path.</summary>
    public string FileName { get; }

    /// <summary>Output size in bytes.</summary>
    public long Size { get; }

    /// <summary>Lowercase SHA-256 hash of the output bytes, or the empty output hash when failed before output.</summary>
    public string Sha256 { get; }

    /// <summary>True when build committed output through the writer port.</summary>
    public bool Committed { get; }
}
