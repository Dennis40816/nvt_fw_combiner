using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Application.Ports;

/// <summary>
/// Reads one selected host artifact and returns content identity plus
/// explicitly non-authoritative presentation hints.
/// </summary>
public interface ISelectedFileContentInspector
{
    /// <summary>
    /// Inspects the complete currently selected file. Identity-only inspection
    /// retains no payload; existing callers capture bytes unless specified otherwise.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>;
    /// unstable content raises <see cref="SelectedFileChangedDuringInspectionException"/>.
    /// Capture is additionally limited by byte-array capacity; identity-only
    /// inspection uses the caller's 64-bit ceiling without that storage limit.
    /// </summary>
    ValueTask<SelectedFileContentInspection> InspectAsync(
        string selectedPath,
        long maximumBytes,
        CancellationToken cancellationToken,
        SelectedFileContentInspectionMode mode = SelectedFileContentInspectionMode.CaptureBytes);
}

/// <summary>Requested payload lifetime for complete selected-file inspection.</summary>
public enum SelectedFileContentInspectionMode
{
    /// <summary>Return the complete length and SHA-256 without retaining file bytes.</summary>
    IdentityOnly,

    /// <summary>Also retain the complete immutable bytes matching the returned stamp.</summary>
    CaptureBytes,
}

/// <summary>Observed instability while reading the admitted complete-file length.</summary>
public enum SelectedFileContentChangeKind
{
    /// <summary>The caller did not supply a more specific observation.</summary>
    Unspecified,

    /// <summary>End of stream was reached before the admitted length.</summary>
    ShortRead,

    /// <summary>A trailing byte or a larger final length was observed.</summary>
    Growth,

    /// <summary>A smaller final length was observed.</summary>
    Shrinkage,

    /// <summary>The final stream position did not match the admitted length.</summary>
    PositionChanged,
}

/// <summary>A selected host file exceeds the inspection or capture-storage ceiling.</summary>
public sealed class SelectedFileSizeLimitExceededException : Exception
{
    /// <summary>Creates one typed pre-hash size rejection.</summary>
    public SelectedFileSizeLimitExceededException(
        long observedBytes,
        long maximumBytes)
        : this(observedBytes, maximumBytes, isCaptureStorageLimit: false)
    {
    }

    /// <summary>Creates a typed size rejection identifying whether capture storage imposed the ceiling.</summary>
    public SelectedFileSizeLimitExceededException(
        long observedBytes,
        long maximumBytes,
        bool isCaptureStorageLimit)
        : base(
            isCaptureStorageLimit
                ? $"Selected file length {observedBytes} exceeds the capture storage limit {maximumBytes} bytes."
                : $"Selected file length {observedBytes} exceeds the resolved maximum {maximumBytes} bytes.")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(observedBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ObservedBytes = observedBytes;
        MaximumBytes = maximumBytes;
        IsCaptureStorageLimit = isCaptureStorageLimit;
    }

    /// <summary>Whole-file length observed before hashing.</summary>
    public long ObservedBytes { get; }

    /// <summary>Inclusive whole-file ceiling imposed by the caller or capture storage.</summary>
    public long MaximumBytes { get; }

    /// <summary>True when capture storage, rather than the caller's resolved ceiling, imposed the limit.</summary>
    public bool IsCaptureStorageLimit { get; }
}

/// <summary>The selected file changed while its admitted bytes were being inspected.</summary>
public sealed class SelectedFileChangedDuringInspectionException : IOException
{
    /// <summary>Creates one typed whole-file stability rejection.</summary>
    public SelectedFileChangedDuringInspectionException(
        SelectedFileContentChangeKind changeKind = SelectedFileContentChangeKind.Unspecified)
        : base("Selected file length changed during complete-content inspection.")
    {
        ChangeKind = changeKind;
    }

    /// <summary>Typed read observation; no successful stamp is published for this read.</summary>
    public SelectedFileContentChangeKind ChangeKind { get; }
}

/// <summary>
/// Immutable host inspection result. <see cref="FileStamp"/> identifies the
/// complete inspected content. Bytes are optional; display name and timestamp
/// are hints only.
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

    /// <summary>Immutable complete captured bytes, or null for identity-only inspection.</summary>
    public ReadOnlyMemory<byte>? AcceptedBytes => AcceptedByteArray is null
        ? (ReadOnlyMemory<byte>?)null
        : new ReadOnlyMemory<byte>(AcceptedByteArray);

    internal byte[]? AcceptedByteArray { get; }
}
