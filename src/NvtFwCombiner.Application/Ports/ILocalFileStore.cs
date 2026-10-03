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

    /// <summary>Atomically publishes one local file using the requested replacement policy.</summary>
    /// <remarks>CreateNew refuses an existing destination at publication, preserving its bytes.</remarks>
    ValueTask WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        LocalFileWriteMode mode,
        CancellationToken cancellationToken);

    /// <summary>Atomically publishes with explicit parent-directory admission and replacement policy.</summary>
    ValueTask WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        LocalFileWriteOptions options,
        CancellationToken cancellationToken);

    /// <summary>Inspects a local destination without creating the parent or destination.</summary>
    ValueTask<LocalFileDestinationInfo> InspectDestinationAsync(string path, CancellationToken cancellationToken);

    /// <summary>Tests physical file identity, including alternate spellings, symbolic links and hard links.</summary>
    /// <remarks>Returns false when either path does not exist. Does not require content-read access.</remarks>
    ValueTask<bool> RefersToSameFileAsync(string first, string second, CancellationToken cancellationToken);
}

/// <summary>Controls atomic local-file publication when the destination already exists.</summary>
public enum LocalFileWriteMode
{
    /// <summary>Replaces an existing destination atomically.</summary>
    ReplaceExisting,

    /// <summary>Publishes only if the destination is absent at the commit boundary.</summary>
    CreateNew,
}

/// <summary>Controls local-file publication and whether its parent must already exist.</summary>
public readonly record struct LocalFileWriteOptions(LocalFileWriteMode Mode, bool RequireExistingParent);

/// <summary>Snapshot of destination and parent admission; publication must recheck its own invariants.</summary>
public readonly record struct LocalFileDestinationInfo(
    bool ParentExists,
    bool IsDirectory,
    bool Exists,
    bool ParentHasExactPath);

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
