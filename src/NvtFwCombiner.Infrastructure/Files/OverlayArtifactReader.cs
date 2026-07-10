using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Reads host-owned immutable in-memory artifacts before delegating to a fallback reader.</summary>
public sealed class OverlayArtifactReader : IArtifactReader
{
    private readonly IArtifactReader _fallback;
    private readonly Dictionary<string, byte[]> _artifacts;

    /// <summary>Creates an immutable artifact overlay over a physical-reader fallback.</summary>
    public OverlayArtifactReader(
        IArtifactReader fallback,
        IEnumerable<KeyValuePair<string, byte[]>> artifacts)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(artifacts);

        _fallback = fallback;
        _artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach ((string artifactId, byte[] bytes) in artifacts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
            ArgumentNullException.ThrowIfNull(bytes);
            if (!_artifacts.TryAdd(artifactId, [.. bytes]))
            {
                throw new ArgumentException($"Artifact '{artifactId}' is declared more than once.", nameof(artifacts));
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        cancellationToken.ThrowIfCancellationRequested();

        return _artifacts.TryGetValue(artifactId, out byte[]? bytes)
            ? ValueTask.FromResult(new ReadOnlyMemory<byte>([.. bytes]))
            : _fallback.ReadAsync(artifactId, cancellationToken);
    }
}
