namespace NvtFwCombiner.Domain.Composition;

/// <summary>Maps one declared address-space slice into a named external-processor staging artifact.</summary>
public sealed class ExternalProcessorStagedArtifactBinding
{
    /// <summary>Creates one named artifact binding.</summary>
    public ExternalProcessorStagedArtifactBinding(string artifactId, string sourceSpaceId, ByteRange sourceRange)
    {
        ExternalProcessorStagedArtifact.ValidateArtifactId(artifactId, nameof(artifactId));
        SourceSpaceId = RequiredValue.NotBlank(sourceSpaceId);

        ArtifactId = artifactId;
        SourceRange = sourceRange;
    }

    /// <summary>Closed staging artifact identifier expected by the selected tool manifest.</summary>
    public string ArtifactId { get; }

    /// <summary>Immutable or engine-owned mutable address space supplying the artifact bytes.</summary>
    public string SourceSpaceId { get; }

    /// <summary>Exact half-open source range staged as the artifact.</summary>
    public ByteRange SourceRange { get; }
}
