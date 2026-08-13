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
        CancellationToken cancellationToken)
    {
        return ReadAsync(path, maximumBytes, ReadTextAsync, cancellationToken);
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
            return await ReadTextAsync(snapshot, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<string> ReadTextAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, StrictUtf8, true, BufferBytes, leaveOpen: true);
        var text = new StringBuilder();
        char[] buffer = new char[BufferBytes];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            _ = text.Append(buffer, 0, read);
        }

        return text.ToString();
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
                new(exception.Message, exception),
            UnauthorizedAccessException or NotSupportedException => new(exception.Message, exception),
            DecoderFallbackException => new(exception.Message, exception),
            IOException => new(exception.Message, exception),
            _ => null,
        };
    }
}
