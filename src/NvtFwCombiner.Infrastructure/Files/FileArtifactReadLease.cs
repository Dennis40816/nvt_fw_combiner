using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Owns a verified staging copy through a read-only, delete-on-close handle.</summary>
internal sealed class FileArtifactReadLease : ArtifactReadLease
{
    private readonly FileStream _sealedCopy;

    private FileArtifactReadLease(FileStream sealedCopy, FileStamp stamp)
        : base(stamp)
    {
        _sealedCopy = sealedCopy;
    }

    /// <summary>
    /// Stages a source positioned at zero without reopening it. The optional
    /// staging opener allows deterministic storage-failure tests of this pipeline.
    /// The caller retains disposal responsibility; file adapters request early
    /// source closure immediately after copying, before staging verification.
    /// </summary>
    internal static async ValueTask<ArtifactReadLease> CreateAsync(
        Stream source,
        string stagingRoot,
        CancellationToken cancellationToken,
        Func<string, FileStream>? openStagingFile = null,
        bool closeSourceAfterCopy = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Position != 0)
        {
            throw new ArgumentException("Artifact staging must start at source offset zero.", nameof(source));
        }
        long length = source.Length;
        string root = FileSystemPathGuard.ResolveExistingRoot(stagingRoot);
        string path = FileSystemPathGuard.ResolveFileNameUnderRoot($"artifact-{Guid.NewGuid():N}.tmp", root);
        FileStream? sealedCopy = null;
        bool ownsStaging = false;
        try
        {
            byte[] sourceHash;
            await using (FileStream staging = (openStagingFile ?? OpenStagingFile)(path))
            {
                ownsStaging = true;
                sourceHash = await CopyAndHashAsync(source, staging, length, cancellationToken).ConfigureAwait(false);
                if (closeSourceAfterCopy)
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }
                await staging.FlushAsync(cancellationToken).ConfigureAwait(false);
                staging.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _ = FileSystemPathGuard.ResolveExistingFileUnderRoots(path, [root]);
            sealedCopy = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.RandomAccess | FileOptions.DeleteOnClose,
                BufferSize = 1,
            });
            RegularFileGuard.RequireOpenHandle(sealedCopy.SafeFileHandle, path);
            byte[] stagedHash = await FileContentSnapshotInspector.HashExactLengthAsync(
                sealedCopy, length, cancellationToken).ConfigureAwait(false);
            if (!sourceHash.AsSpan().SequenceEqual(stagedHash))
            {
                throw new IOException("Artifact staging verification failed.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new FileArtifactReadLease(sealedCopy, new FileStamp(length, Convert.ToHexStringLower(sourceHash)));
        }
        catch
        {
            if (sealedCopy is not null)
            {
                await sealedCopy.DisposeAsync().ConfigureAwait(false);
            }
            if (ownsStaging)
            {
                File.Delete(path);
            }
            throw;
        }
    }

    private static FileStream OpenStagingFile(string path)
    {
        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 1,
        });
    }

    private static async ValueTask<byte[]> CopyAndHashAsync(
        Stream source, Stream staging, long length, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1024 * 1024];
        long offset = 0;
        while (offset < length)
        {
            int requested = (int)Math.Min(buffer.Length, checked(length - offset));
            int read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0)
            {
                throw new SelectedFileChangedDuringInspectionException(SelectedFileContentChangeKind.ShortRead);
            }
            hash.AppendData(buffer.AsSpan(0, read));
            await staging.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            offset = checked(offset + read);
        }

        int trailing = await source.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return trailing != 0 || source.Length != length || source.Position != length
            ? throw new SelectedFileChangedDuringInspectionException()
            : hash.GetHashAndReset();
    }

    /// <inheritdoc />
    protected override async ValueTask ReadRangeAsync(
        long offset, Memory<byte> destination, CancellationToken cancellationToken)
    {
        while (!destination.IsEmpty)
        {
            int read = await RandomAccess.ReadAsync(
                _sealedCopy.SafeFileHandle, destination, offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The sealed artifact ended inside an admitted range.");
            }
            offset = checked(offset + read);
            destination = destination[read..];
        }
    }

    /// <inheritdoc />
    protected override void DisposeCore()
    {
        _sealedCopy.Dispose();
    }
}
