using System.Collections.Concurrent;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.TestSupport;

/// <summary>
/// Records every path a composition asks the local-file port to read or write without touching the file system.
/// Reads report a missing file and writes are only recorded, so a test can prove where local state would go.
/// </summary>
public sealed class RecordingLocalFileStore : ILocalFileStore
{
    private readonly ConcurrentQueue<string> _reads = new();
    private readonly ConcurrentQueue<string> _writes = new();

    /// <summary>Gets the full paths requested for reading, in request order.</summary>
    public IReadOnlyList<string> Reads => [.. _reads];

    /// <summary>Gets the full paths requested for writing, in request order.</summary>
    public IReadOnlyList<string> Writes => [.. _writes];

    /// <summary>Gets every requested read and write path.</summary>
    public IReadOnlyList<string> Paths => [.. _reads, .. _writes];

    /// <inheritdoc />
    public ValueTask<T> ReadAsync<T>(
        string path,
        long maximumBytes,
        Func<Stream, CancellationToken, ValueTask<T>> project,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reads.Enqueue(Path.GetFullPath(path));
        throw new LocalFileNotFoundException("The recording store holds no files.");
    }

    /// <inheritdoc />
    public ValueTask<string> ReadTextAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken,
        Action<LocalFileReadProgress>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reads.Enqueue(Path.GetFullPath(path));
        throw new LocalFileNotFoundException("The recording store holds no files.");
    }

    /// <inheritdoc />
    public ValueTask<string> ReadTextAsync(
        Func<CancellationToken, ValueTask<Stream>> openReadAsync,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("The recording store only observes path-addressed local state.");
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writes.Enqueue(Path.GetFullPath(path));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> RefersToSameFileAsync(string first, string second, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
        LocalFileWriteMode mode, CancellationToken cancellationToken)
    {
        return WriteAsync(path, bytes, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
        LocalFileWriteOptions options, CancellationToken cancellationToken)
    {
        return WriteAsync(path, bytes, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<LocalFileDestinationInfo> InspectDestinationAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new LocalFileDestinationInfo(true, false, false, true));
    }
}
