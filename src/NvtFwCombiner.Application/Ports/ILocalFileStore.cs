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

    /// <summary>Reads one stable bounded path as UTF text.</summary>
    ValueTask<string> ReadTextAsync(string path, long maximumBytes, CancellationToken cancellationToken);

    /// <summary>Reads one storage-provider stream as bounded UTF text.</summary>
    ValueTask<string> ReadTextAsync(
        Func<CancellationToken, ValueTask<Stream>> openReadAsync,
        long maximumBytes,
        CancellationToken cancellationToken);

    /// <summary>Atomically replaces one local file.</summary>
    ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
}

/// <summary>A local file could not be admitted or read safely.</summary>
public class LocalFileReadException(string message, Exception? innerException = null)
    : IOException(message, innerException);

/// <summary>A bounded local file exceeds the caller-owned byte ceiling.</summary>
public sealed class LocalFileTooLargeException(string message) : LocalFileReadException(message);
