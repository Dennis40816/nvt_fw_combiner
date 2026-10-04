namespace NvtFwCombiner.Application.Ports;

/// <summary>Reads immutable input artifacts for application use cases.</summary>
public interface IArtifactReader
{
    /// <summary>Reads the artifact bytes identified by the typed request.</summary>
    ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the complete artifact once into host-owned staging under the caller's
    /// existing root, verifies and seals it, then returns bounded reads and its
    /// complete stamp. The source is closed before return. Disposal deletes the
    /// copy; failure or cancellation cleans staging. Application retains stamp
    /// admission. Existing materialization-only readers remain compatible and
    /// explicitly reject this additional capability.
    /// </summary>
    ValueTask<ArtifactReadLease> OpenReadLeaseAsync(
        string artifactId,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromException<ArtifactReadLease>(
            new NotSupportedException("This artifact reader does not support immutable range leases."));
    }
}
