using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.TestSupport;

/// <summary>In-memory artifact reader for deterministic application tests.</summary>
public sealed class FakeArtifactReader : IArtifactReader
{
    private readonly IReadOnlyDictionary<string, byte[]> _artifacts;

    /// <summary>Creates a reader over artifact bytes keyed by artifact id.</summary>
    public FakeArtifactReader(Dictionary<string, byte[]> artifacts)
        : this((IReadOnlyDictionary<string, byte[]>)artifacts)
    {
    }

    /// <summary>Creates a reader over artifact bytes keyed by artifact id.</summary>
    public FakeArtifactReader(IReadOnlyDictionary<string, byte[]> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        _artifacts = artifacts;
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(_artifacts[artifactId]);
    }
}
