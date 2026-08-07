namespace NvtFwCombiner.Domain.Composition;

/// <summary>Maps one declared address-space slice into a named external-processor staging artifact.</summary>
public sealed class ExternalProcessorStagedArtifactBinding(
    string artifactId,
    string sourceSpaceId,
    ByteRange sourceRange)
{
    /// <summary>Closed staging artifact identifier expected by the selected tool manifest.</summary>
    public string ArtifactId { get; } = RequireArtifactId(artifactId);

    /// <summary>Immutable or engine-owned mutable address space supplying the artifact bytes.</summary>
    public string SourceSpaceId { get; } = RequiredValue.NotBlank(sourceSpaceId);

    /// <summary>Exact half-open source range staged as the artifact.</summary>
    public ByteRange SourceRange { get; } = sourceRange;

    private static string RequireArtifactId(string artifactId)
    {
        ExternalProcessorStagedArtifact.ValidateArtifactId(artifactId, nameof(artifactId));
        return artifactId;
    }
}
