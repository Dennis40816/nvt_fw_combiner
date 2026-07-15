namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe summary of one bound input artifact.</summary>
public sealed class InputArtifactSummary
{
    /// <summary>Creates an input summary without a host path.</summary>
    public InputArtifactSummary(
        string addressSpaceId,
        string artifactId,
        long size,
        string sha256,
        string? originalFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (originalFileName is not null &&
            (string.IsNullOrWhiteSpace(originalFileName) ||
             originalFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
             originalFileName is "." or ".." ||
             originalFileName.Any(char.IsControl) ||
             Path.GetFileName(originalFileName) != originalFileName))
        {
            throw new ArgumentException(
                "Original file name must be a plain filename without path or control syntax.",
                nameof(originalFileName));
        }

        AddressSpaceId = addressSpaceId;
        ArtifactId = artifactId;
        Size = size;
        Sha256 = sha256;
        OriginalFileName = originalFileName;
    }

    /// <summary>Address space populated by this artifact.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Application-local artifact id, not a portable file path.</summary>
    public string ArtifactId { get; }

    /// <summary>Input size in bytes.</summary>
    public long Size { get; }

    /// <summary>Lowercase SHA-256 hash of the input bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Original plain filename retained when the request provides V2 input provenance.</summary>
    public string? OriginalFileName { get; }
}
