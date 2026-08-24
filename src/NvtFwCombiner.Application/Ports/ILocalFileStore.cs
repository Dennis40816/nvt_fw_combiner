namespace NvtFwCombiner.Application.Ports;

/// <summary>Provides bounded stable local-file reads and atomic writes.</summary>
public interface ILocalFileStore
{
    /// <summary>Reads and projects one stable bounded path.</summary>
    ValueTask<T> ReadAsync<T>(
        string path,
        long maximumBytes,
        Func<Stream, CancellationToken, ValueTask<T>> project,
        CancellationToken cancellationToken);

    /// <summary>Reads one stable bounded path as UTF text; progress is monotonic with a stable admitted total.</summary>
    ValueTask<string> ReadTextAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken,
        Action<LocalFileReadProgress>? progress = null);

    /// <summary>Reads one storage-provider stream as bounded UTF text.</summary>
    ValueTask<string> ReadTextAsync(
        Func<CancellationToken, ValueTask<Stream>> openReadAsync,
        long maximumBytes,
        CancellationToken cancellationToken);

    /// <summary>Atomically replaces one local file.</summary>
    ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
}

/// <summary>Admitted text bytes consumed so far; total remains stable and progress is monotonic.</summary>
public readonly record struct LocalFileReadProgress(long BytesRead, long TotalBytes);

/// <summary>A local file could not be admitted or read safely.</summary>
public class LocalFileReadException(string message, Exception? innerException = null)
    : IOException(message, innerException);

/// <summary>The requested local file or one of its parent directories does not exist.</summary>
public sealed class LocalFileNotFoundException(string message, Exception? innerException = null)
    : LocalFileReadException(message, innerException);

/// <summary>A bounded local file exceeds the caller-owned byte ceiling.</summary>
public sealed class LocalFileTooLargeException(string message) : LocalFileReadException(message);
