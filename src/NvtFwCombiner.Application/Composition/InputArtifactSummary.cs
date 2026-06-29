namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe summary of one bound input artifact.</summary>
public sealed class InputArtifactSummary
{
    /// <summary>Creates an input summary without a host path.</summary>
    public InputArtifactSummary(string addressSpaceId, string artifactId, long size, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        AddressSpaceId = addressSpaceId;
        ArtifactId = artifactId;
        Size = size;
        Sha256 = sha256;
    }

    /// <summary>Address space populated by this artifact.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Application-local artifact id, not a portable file path.</summary>
    public string ArtifactId { get; }

    /// <summary>Input size in bytes.</summary>
    public long Size { get; }

    /// <summary>Lowercase SHA-256 hash of the input bytes.</summary>
    public string Sha256 { get; }
}
