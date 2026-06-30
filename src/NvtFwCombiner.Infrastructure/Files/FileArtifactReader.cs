using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Reads host files as immutable application artifacts under configured roots.</summary>
public sealed class FileArtifactReader : IArtifactReader
{
    private readonly string[] _allowedRoots;

    /// <summary>Creates a file reader constrained to one or more allowed roots.</summary>
    public FileArtifactReader(IEnumerable<string> allowedRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedRoots);

        _allowedRoots = [.. allowedRoots.Select(FileSystemPathGuard.ResolveExistingRoot)];
        if (_allowedRoots.Length == 0)
        {
            throw new ArgumentException("At least one allowed root is required.", nameof(allowedRoots));
        }
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        string artifactId,
        CancellationToken cancellationToken)
    {
        string path = FileSystemPathGuard.ResolveExistingFileUnderRoots(artifactId, _allowedRoots);
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes;
    }
}
