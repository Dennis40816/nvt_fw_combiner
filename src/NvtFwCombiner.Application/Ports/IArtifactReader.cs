namespace NvtFwCombiner.Application.Ports;

/// <summary>Reads immutable input artifacts for application use cases.</summary>
public interface IArtifactReader
{
    /// <summary>Reads the artifact bytes identified by the typed request.</summary>
    ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken);
}
