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
        CancellationToken cancellationToken)
    {
        string path = FileSystemPathGuard.ResolveExistingFileUnderRoots(
            selectedPath,
            _allowedRoots);
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        return new SelectedFileContentInspection(
            FileStamp.FromBytes(bytes),
            Path.GetFileName(path));
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
        CancellationToken cancellationToken)
    {
        SelectedFileContentInspection inspected =
            await _contentInspector.InspectAsync(selectedPath, cancellationToken)
                .ConfigureAwait(false);
        DateTimeOffset lastWriteTimeUtcHint =
            new(File.GetLastWriteTimeUtc(Path.GetFullPath(selectedPath)), TimeSpan.Zero);
        return new SelectedFileContentInspection(
            inspected.FileStamp,
            inspected.DisplayNameHint,
            lastWriteTimeUtcHint);
    }
}
