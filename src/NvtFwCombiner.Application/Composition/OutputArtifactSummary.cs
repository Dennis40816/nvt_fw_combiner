namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe summary of composition output.</summary>
public sealed class OutputArtifactSummary(
    string fileName,
    long size,
    string sha256,
    bool committed)
{
    /// <summary>Output file name, without a host path.</summary>
    public string FileName { get; } = CompositionSummaryValue.NotBlank(fileName, nameof(fileName));

    /// <summary>Output size in bytes.</summary>
    public long Size { get; } = CompositionSummaryValue.NonNegative(size, nameof(size));

    /// <summary>Lowercase SHA-256 hash of the output bytes, or the empty output hash when failed before output.</summary>
    public string Sha256 { get; } = CompositionSummaryValue.NotBlank(sha256, nameof(sha256));

    /// <summary>True when build committed output through the writer port.</summary>
    public bool Committed { get; } = committed;
}
