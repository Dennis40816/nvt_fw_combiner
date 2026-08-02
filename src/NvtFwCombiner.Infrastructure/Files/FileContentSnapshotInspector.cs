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

        byte[] sha256 = await HashExactLengthAsync(
                stream,
                observedLength,
                cancellationToken)
            .ConfigureAwait(false);
        return stream.Position == observedLength && stream.Length == observedLength
            ? new SelectedFileContentInspection(
                new FileStamp(
                    observedLength,
                    Convert.ToHexStringLower(sha256)),
                Path.GetFileName(path))
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
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(observedLength);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        long remaining = observedLength;
        while (remaining > 0)
        {
            int read = await stream.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException(
                    "Selected file length changed during complete-content inspection.");
            }

            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        int trailingRead = await stream.ReadAsync(
                buffer.AsMemory(0, 1),
                cancellationToken)
            .ConfigureAwait(false);
        return trailingRead == 0
            ? hash.GetHashAndReset()
            : throw new IOException(
                "Selected file length changed during complete-content inspection.");
    }
}

/// <summary>
/// Named one-way adapter for legacy callers that still expect a filesystem
/// timestamp capture step. It never converts a timestamp into identity:
/// accepted identity is always projected through complete content inspection.
///
/// Caller inventory: no General Merge, General Replace, CLI, or Saved Rule
/// binding caller. Retain only while non-General host selection boundaries
/// migrate. Delete when those boundaries use <see cref="FileContentSnapshotInspector"/>
/// directly.
/// </summary>
public sealed class LegacyTimestampFileStampCompatibilityAdapter
    : ISelectedFileContentInspector
{
    private readonly FileContentSnapshotInspector _contentInspector;

    /// <summary>Creates the one-way compatibility projection.</summary>
    public LegacyTimestampFileStampCompatibilityAdapter(
        IEnumerable<string> allowedRoots)
    {
        _contentInspector = new FileContentSnapshotInspector(allowedRoots);
    }

    /// <inheritdoc />
    public async ValueTask<SelectedFileContentInspection> InspectAsync(
        string selectedPath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        SelectedFileContentInspection inspected =
            await _contentInspector.InspectAsync(
                    selectedPath,
                    maximumBytes,
                    cancellationToken)
                .ConfigureAwait(false);
        DateTimeOffset lastWriteTimeUtcHint =
            new(File.GetLastWriteTimeUtc(Path.GetFullPath(selectedPath)), TimeSpan.Zero);
        return new SelectedFileContentInspection(
            inspected.FileStamp,
            inspected.DisplayNameHint,
            lastWriteTimeUtcHint);
    }
}
