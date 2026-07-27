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

    /// <summary>
    /// Reads one immutable artifact while holding both it and a protected artifact open, so the
    /// consumed bytes cannot alias the protected file or change identity between admission and read.
    /// </summary>
    public async ValueTask<ReadOnlyMemory<byte>> ReadDistinctAsync(
        string artifactId,
        string protectedArtifactId,
        CancellationToken cancellationToken)
    {
        string path = FileSystemPathGuard.ResolveExistingFileUnderRoots(artifactId, _allowedRoots);
        string protectedPath = FileSystemPathGuard.ResolveExistingFileUnderRoots(
            protectedArtifactId,
            _allowedRoots);
        await using FileStream stream = OpenSnapshotStream(path);
        await using FileStream protectedStream = OpenDistinctProtectedStream(protectedPath, path);
        byte[] bytes = await ReadStableSnapshotAsync(stream, path, cancellationToken).ConfigureAwait(false);
        _ = FileSystemPathGuard.ResolveExistingFileUnderRoots(path, _allowedRoots);
        _ = FileSystemPathGuard.ResolveExistingFileUnderRoots(protectedPath, _allowedRoots);
        return bytes;
    }

    private static FileStream OpenSnapshotStream(string path)
    {
        var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 4096,
        });
        try
        {
            RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, path);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static FileStream OpenDistinctProtectedStream(string protectedPath, string artifactPath)
    {
        try
        {
            var stream = new FileStream(protectedPath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 1,
            });
            try
            {
                RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, protectedPath);
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException(
                $"Could not prove artifact '{artifactPath}' is physically distinct from protected artifact '{protectedPath}'.",
                nameof(protectedPath),
                exception);
        }
    }

    private static async ValueTask<byte[]> ReadStableSnapshotAsync(
        FileStream stream,
        string displayPath,
        CancellationToken cancellationToken)
    {
        long length = stream.Length;
        byte[] bytes = new byte[checked((int)length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return stream.ReadByte() != -1 || stream.Length != length
            ? throw new IOException($"Artifact file '{displayPath}' changed while it was being read.")
            : bytes;
    }
}
