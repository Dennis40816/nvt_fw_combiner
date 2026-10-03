using System.Text;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Platform bounded local-file adapter.</summary>
public sealed class LocalFileStore : ILocalFileStore
{
    private const int BufferBytes = 64 * 1024;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <inheritdoc />
    public async ValueTask<T> ReadAsync<T>(
        string path,
        long maximumBytes,
        Func<Stream, CancellationToken, ValueTask<T>> project,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        try
        {
            await using var stream = new FileStream(
                Path.GetFullPath(path),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                BufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, path);
            long admittedLength = stream.Length;
            DateTime admittedLastWriteTimeUtc = File.GetLastWriteTimeUtc(stream.SafeFileHandle);
            EnsureAccepted(admittedLength, maximumBytes);
            T result = await project(stream, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return stream.Length == admittedLength &&
                   File.GetLastWriteTimeUtc(stream.SafeFileHandle) == admittedLastWriteTimeUtc
                ? result
                : throw new IOException("The local file changed during the stable read.");
        }
        catch (Exception exception) when (Wrap(exception) is { } wrapped)
        {
            throw wrapped;
        }
    }

    /// <inheritdoc />
    public ValueTask<string> ReadTextAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken,
        Action<LocalFileReadProgress>? progress = null)
    {
        return ReadAsync(path, maximumBytes,
            (stream, token) => ReadTextAsync(stream, progress, token), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<string> ReadTextAsync(
        Func<CancellationToken, ValueTask<Stream>> openReadAsync,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(openReadAsync);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        try
        {
            await using Stream source = await openReadAsync(cancellationToken).ConfigureAwait(false) ??
                throw new IOException("The selected source returned no readable stream.");
            bool seekable = source.CanSeek;
            long admittedLength = seekable ? source.Length - source.Position : 0;
            if (admittedLength < 0)
            {
                throw new IOException("The selected source has an invalid stream position.");
            }

            EnsureAccepted(admittedLength, maximumBytes);

            using var snapshot = new MemoryStream();
            byte[] buffer = new byte[BufferBytes];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                EnsureAccepted(checked(snapshot.Length + read), maximumBytes);
                await snapshot.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            if (seekable && snapshot.Length != admittedLength)
            {
                throw new IOException("The selected source changed during the stable read.");
            }

            snapshot.Position = 0;
            return await ReadTextAsync(snapshot, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (Wrap(exception) is { } wrapped)
        {
            throw wrapped;
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using IAtomicFileWriteScope writeScope = AtomicFileWriteScope.Open(fullPath);
        await writeScope.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        LocalFileWriteMode mode,
        CancellationToken cancellationToken)
    {
        return WriteAsync(path, bytes, new LocalFileWriteOptions(mode, false), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        LocalFileWriteOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Mode is not (LocalFileWriteMode.ReplaceExisting or LocalFileWriteMode.CreateNew))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        string fullPath = Path.GetFullPath(path);
        string parent = Path.GetDirectoryName(fullPath)!;
        if (options.RequireExistingParent)
        {
            if (!Directory.Exists(parent))
            {
                throw new DirectoryNotFoundException($"Local file target directory was not found: {parent}");
            }
        }
        else
        {
            _ = Directory.CreateDirectory(parent);
        }
        using IAtomicFileWriteScope writeScope = AtomicFileWriteScope.Open(fullPath);
        await writeScope.WriteAsync(bytes, options.Mode, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<LocalFileDestinationInfo> InspectDestinationAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        string parent = Path.GetDirectoryName(fullPath)!;
        bool parentExists = Directory.Exists(parent);
        bool exactParent = parentExists && (OperatingSystem.IsWindows()
            ? WindowsAtomicFileWriteScope.HasExactDirectoryPath(parent)
            : UnixAtomicFileWriteScope.HasExactDirectoryPath(parent));
        return ValueTask.FromResult(new LocalFileDestinationInfo(
            parentExists,
            Directory.Exists(fullPath),
            File.Exists(fullPath) || new FileInfo(fullPath).LinkTarget is not null,
            exactParent));
    }

    /// <inheritdoc />
    public ValueTask<bool> RefersToSameFileAsync(string first, string second, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(first);
        ArgumentException.ThrowIfNullOrWhiteSpace(second);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LocalFileIdentity.RefersToSameFile(Path.GetFullPath(first), Path.GetFullPath(second)));
    }

    private static async ValueTask<string> ReadTextAsync(
        Stream stream,
        Action<LocalFileReadProgress>? observer,
        CancellationToken cancellationToken)
    {
        long total = stream.Length;
        ReportProgress(observer, new(0, total));
        long reported = 0;
        using var reader = new StreamReader(stream, StrictUtf8, true, BufferBytes, leaveOpen: true);
        var text = new StringBuilder();
        char[] buffer = new char[BufferBytes];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            _ = text.Append(buffer, 0, read);
            long completed = Math.Min(stream.Position, total);
            if (completed != reported)
            {
                ReportProgress(observer, new(completed, total));
                reported = completed;
            }
        }
        return text.ToString();
    }

    private static void ReportProgress(Action<LocalFileReadProgress>? observer, LocalFileReadProgress update)
    {
        if (observer is null)
        {
            return;
        }
        try
        {
            observer(update);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException("The local-file progress observer failed.", exception);
        }
    }

    private static void EnsureAccepted(long length, long maximumBytes)
    {
        if (length > maximumBytes)
        {
            throw new LocalFileTooLargeException(
                $"File length {length} exceeds the {maximumBytes}-byte limit.");
        }
    }

    private static LocalFileReadException? Wrap(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => null,
            LocalFileReadException => null,
            FileNotFoundException or DirectoryNotFoundException =>
                new LocalFileNotFoundException(exception.Message, exception),
            UnauthorizedAccessException or NotSupportedException =>
                new LocalFileReadException(exception.Message, exception),
            DecoderFallbackException => new LocalFileReadException(exception.Message, exception),
            IOException => new LocalFileReadException(exception.Message, exception),
            _ => null,
        };
    }
}
