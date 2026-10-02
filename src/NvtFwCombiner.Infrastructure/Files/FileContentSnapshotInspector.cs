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
    private readonly string[]? _allowedRoots;

    internal FileContentSnapshotInspector()
    {
    }

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
        CancellationToken cancellationToken,
        SelectedFileContentInspectionMode mode = SelectedFileContentInspectionMode.CaptureBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ValidateMode(mode);
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(selectedPath);
        string path = FileSystemPathGuard.ResolveExistingFileUnderRoots(
            fullPath,
            _allowedRoots ?? [FileSystemPathGuard.ResolveExistingRoot(Path.GetDirectoryName(fullPath)!)]);
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

        (byte[]? acceptedBytes, byte[] sha256) = await ReadAndHashExactLengthAsync(
                stream,
                observedLength,
                mode,
                cancellationToken)
            .ConfigureAwait(false);
        return new SelectedFileContentInspection(
            new FileStamp(
                observedLength,
                Convert.ToHexStringLower(sha256)),
            Path.GetFileName(path),
            acceptedBytes: acceptedBytes is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(acceptedBytes));
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
                SelectedFileContentInspectionMode.IdentityOnly,
                cancellationToken)
            .ConfigureAwait(false);
        return sha256;
    }

    internal static async ValueTask<(byte[]? AcceptedBytes, byte[] Sha256)>
        ReadAndHashExactLengthAsync(
            Stream stream,
            long observedLength,
            SelectedFileContentInspectionMode mode,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(observedLength);
        ValidateMode(mode);
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == SelectedFileContentInspectionMode.CaptureBytes && observedLength > Array.MaxLength)
        {
            throw new SelectedFileSizeLimitExceededException(observedLength, Array.MaxLength);
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[]? acceptedBytes = mode == SelectedFileContentInspectionMode.CaptureBytes
            ? new byte[checked((int)observedLength)]
            : null;
        byte[] buffer = new byte[64 * 1024];
        long offset = 0;
        while (offset < observedLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int requested = (int)Math.Min(buffer.Length, checked(observedLength - offset));
            int read = await stream.ReadAsync(
                    buffer.AsMemory(0, requested),
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0)
            {
                throw new SelectedFileChangedDuringInspectionException(
                    SelectedFileContentChangeKind.ShortRead);
            }

            hash.AppendData(buffer, 0, read);
            if (acceptedBytes is not null)
            {
                buffer.AsMemory(0, read).CopyTo(acceptedBytes.AsMemory(checked((int)offset), read));
            }
            offset = checked(offset + read);
        }

        cancellationToken.ThrowIfCancellationRequested();
        int trailingRead = await stream.ReadAsync(
                buffer.AsMemory(0, 1),
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (trailingRead != 0)
        {
            throw new SelectedFileChangedDuringInspectionException(SelectedFileContentChangeKind.Growth);
        }
        if (stream.CanSeek)
        {
            long finalLength = stream.Length;
            if (finalLength != observedLength)
            {
                throw new SelectedFileChangedDuringInspectionException(
                    finalLength < observedLength
                        ? SelectedFileContentChangeKind.Shrinkage
                        : SelectedFileContentChangeKind.Growth);
            }
            if (stream.Position != observedLength)
            {
                throw new SelectedFileChangedDuringInspectionException(
                    SelectedFileContentChangeKind.PositionChanged);
            }
        }
        return (acceptedBytes, hash.GetHashAndReset());
    }

    private static void ValidateMode(SelectedFileContentInspectionMode mode)
    {
        if (mode is not (SelectedFileContentInspectionMode.IdentityOnly or
            SelectedFileContentInspectionMode.CaptureBytes))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }
}
