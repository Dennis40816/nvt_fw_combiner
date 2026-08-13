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
        long maximumBytes,
        CancellationToken cancellationToken);
}

/// <summary>A selected host file exceeds the caller-resolved inspection ceiling.</summary>
public sealed class SelectedFileSizeLimitExceededException : Exception
{
    /// <summary>Creates one typed pre-hash size rejection.</summary>
    public SelectedFileSizeLimitExceededException(
        long observedBytes,
        long maximumBytes)
        : base(
            $"Selected file length {observedBytes} exceeds the resolved maximum {maximumBytes} bytes.")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(observedBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ObservedBytes = observedBytes;
        MaximumBytes = maximumBytes;
    }

    /// <summary>Whole-file length observed before hashing.</summary>
    public long ObservedBytes { get; }

    /// <summary>Caller-resolved inclusive whole-file ceiling.</summary>
    public long MaximumBytes { get; }
}

/// <summary>The selected file changed while its admitted bytes were being inspected.</summary>
public sealed class SelectedFileChangedDuringInspectionException : IOException
{
    /// <summary>Creates one typed whole-file stability rejection.</summary>
    public SelectedFileChangedDuringInspectionException()
        : base("Selected file length changed during complete-content inspection.")
    {
    }
}

/// <summary>
/// Immutable host inspection result. <see cref="FileStamp"/> identifies the
/// retained accepted bytes; display name and timestamp are hints only.
/// </summary>
public sealed record SelectedFileContentInspection
{
    /// <summary>Creates one content-authoritative inspection result.</summary>
    public SelectedFileContentInspection(
        FileStamp fileStamp,
        string? displayNameHint = null,
        DateTimeOffset? lastWriteTimeUtcHint = null,
        ReadOnlyMemory<byte>? acceptedBytes = null)
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
        AcceptedByteArray = acceptedBytes?.ToArray();
        if (AcceptedByteArray is not null &&
            FileStamp.FromBytes(AcceptedByteArray) != fileStamp)
        {
            throw new ArgumentException(
                "Accepted bytes must match the inspected file identity.",
                nameof(acceptedBytes));
        }
    }

    /// <summary>Accepted complete-file content identity.</summary>
    public FileStamp FileStamp { get; }

    /// <summary>Non-authoritative plain display-name hint.</summary>
    public string? DisplayNameHint { get; }

    /// <summary>Non-authoritative UTC filesystem timestamp hint.</summary>
    public DateTimeOffset? LastWriteTimeUtcHint { get; }

    /// <summary>Immutable complete bytes captured by this exact inspection.</summary>
    public ReadOnlyMemory<byte>? AcceptedBytes => AcceptedByteArray is null
        ? null
        : new ReadOnlyMemory<byte>(AcceptedByteArray);

    internal byte[]? AcceptedByteArray { get; }
}
