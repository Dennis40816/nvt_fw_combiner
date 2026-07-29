using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Application.Ports;

/// <summary>
/// Reads one selected host artifact and returns content identity plus
/// explicitly non-authoritative presentation hints.
/// </summary>
public interface ISelectedFileContentInspector
{
    /// <summary>Inspects the complete currently selected file exactly once.</summary>
    ValueTask<SelectedFileContentInspection> InspectAsync(
        string selectedPath,
        CancellationToken cancellationToken);
}

/// <summary>
/// Immutable host inspection result. Only <see cref="FileStamp"/> participates
/// in accepted byte identity; the remaining values are hints.
/// </summary>
public sealed record SelectedFileContentInspection
{
    /// <summary>Creates one content-authoritative inspection result.</summary>
    public SelectedFileContentInspection(
        FileStamp fileStamp,
        string? displayNameHint = null,
        DateTimeOffset? lastWriteTimeUtcHint = null)
    {
        if (displayNameHint is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayNameHint);
        }

        if (lastWriteTimeUtcHint is { } timestamp &&
            timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Last-write time hints must be normalized to UTC.",
                nameof(lastWriteTimeUtcHint));
        }

        FileStamp = fileStamp;
        DisplayNameHint = displayNameHint;
        LastWriteTimeUtcHint = lastWriteTimeUtcHint;
    }

    /// <summary>Accepted complete-file content identity.</summary>
    public FileStamp FileStamp { get; }

    /// <summary>Non-authoritative plain display-name hint.</summary>
    public string? DisplayNameHint { get; }

    /// <summary>Non-authoritative UTC filesystem timestamp hint.</summary>
    public DateTimeOffset? LastWriteTimeUtcHint { get; }
}
