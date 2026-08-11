using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>
/// Inspects complete host-file content under configured roots. Filesystem
/// names and timestamps are returned only as presentation hints.
/// </summary>
public sealed class FileContentSnapshotInspector
    : ISelectedFileContentInspector
{
    private readonly string[] _allowedRoots;

    /// <summary>Creates a selected-file inspector constrained to allowed roots.</summary>
    public FileContentSnapshotInspector(IEnumerable<string> allowedRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedRoots);
        _allowedRoots =
        [
            .. allowedRoots.Select(FileSystemPathGuard.ResolveExistingRoot),
        ];
        if (_allowedRoots.Length == 0)
        {
            throw new ArgumentException(
                "At least one allowed root is required.",
                nameof(allowedRoots));
        }
    }

    /// <inheritdoc />
    public async ValueTask<SelectedFileContentInspection> InspectAsync(
        string selectedPath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        string path = FileSystemPathGuard.ResolveExistingFileUnderRoots(
            selectedPath,
            _allowedRoots);
        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 64 * 1024,
            });
        long observedLength = stream.Length;
        if (observedLength > maximumBytes)
        {
            throw new SelectedFileSizeLimitExceededException(
                observedLength,
                maximumBytes);
        }

        (byte[] acceptedBytes, byte[] sha256) = await ReadAndHashExactLengthAsync(
                stream,
                observedLength,
                cancellationToken)
            .ConfigureAwait(false);
        return stream.Position == observedLength && stream.Length == observedLength
            ? new SelectedFileContentInspection(
                new FileStamp(
                    observedLength,
                    Convert.ToHexStringLower(sha256)),
                Path.GetFileName(path),
                acceptedBytes: acceptedBytes)
            : throw new IOException(
                "Selected file length changed during complete-content inspection.");
    }

    /// <summary>
    /// Hashes exactly the admitted length and probes at most one trailing byte,
    /// so concurrent growth cannot turn inspection into an unbounded read.
    /// </summary>
    internal static async ValueTask<byte[]> HashExactLengthAsync(
        Stream stream,
        long observedLength,
        CancellationToken cancellationToken)
    {
        (_, byte[] sha256) = await ReadAndHashExactLengthAsync(
                stream,
                observedLength,
                cancellationToken)
            .ConfigureAwait(false);
        return sha256;
    }

    private static async ValueTask<(byte[] AcceptedBytes, byte[] Sha256)>
        ReadAndHashExactLengthAsync(
            Stream stream,
            long observedLength,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(observedLength);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] acceptedBytes = new byte[checked((int)observedLength)];
        int offset = 0;
        while (offset < acceptedBytes.Length)
        {
            int read = await stream.ReadAsync(
                    acceptedBytes.AsMemory(
                        offset,
                        Math.Min(64 * 1024, acceptedBytes.Length - offset)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException(
                    "Selected file length changed during complete-content inspection.");
            }

            hash.AppendData(acceptedBytes, offset, read);
            offset += read;
        }

        byte[] trailing = new byte[1];
        int trailingRead = await stream.ReadAsync(
                trailing,
                cancellationToken)
            .ConfigureAwait(false);
        return trailingRead == 0
            ? (acceptedBytes, hash.GetHashAndReset())
            : throw new IOException(
                "Selected file length changed during complete-content inspection.");
    }
}
