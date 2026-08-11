using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// One immutable Application result shared by General authoring consumers.
/// Definition and revision guard publication; <see cref="FileStamp"/> identifies
/// the retained immutable bytes used by execution.
/// </summary>
public sealed class GeneralSelectedFileInspection(
    string definitionId,
    AuthoringRevision authoringRevision,
    string selectedPathHint,
    FileStamp fileStamp,
    string? displayNameHint = null,
    DateTimeOffset? lastWriteTimeUtcHint = null,
    ReadOnlyMemory<byte>? acceptedBytes = null)
{
    /// <summary>Resolved slot or mapping definition inspected.</summary>
    public string DefinitionId { get; } = !string.IsNullOrWhiteSpace(definitionId)
        ? definitionId
        : throw new ArgumentException("Definition id is required.", nameof(definitionId));

    /// <summary>Authoring revision for which the inspection was requested.</summary>
    public AuthoringRevision AuthoringRevision { get; } = authoringRevision;

    /// <summary>Non-authoritative selected-path hint used for stale-result rejection.</summary>
    public string SelectedPathHint { get; } = !string.IsNullOrWhiteSpace(selectedPathHint)
        ? selectedPathHint
        : throw new ArgumentException("Selected path hint is required.", nameof(selectedPathHint));

    /// <summary>Accepted length and SHA-256 content identity.</summary>
    public FileStamp FileStamp { get; } = fileStamp;

    internal byte[]? AcceptedByteArray { get; } = acceptedBytes?.ToArray();

    /// <summary>Immutable complete bytes captured by this exact inspection.</summary>
    public ReadOnlyMemory<byte>? AcceptedBytes => AcceptedByteArray is null
        ? null
        : new ReadOnlyMemory<byte>(AcceptedByteArray);

    /// <summary>Non-authoritative display-name hint.</summary>
    public string? DisplayNameHint { get; } = displayNameHint is null ||
        !string.IsNullOrWhiteSpace(displayNameHint)
            ? displayNameHint
            : throw new ArgumentException("Display-name hint cannot be blank.", nameof(displayNameHint));

    /// <summary>Non-authoritative filesystem timestamp hint.</summary>
    public DateTimeOffset? LastWriteTimeUtcHint { get; } = lastWriteTimeUtcHint is null ||
        lastWriteTimeUtcHint.Value.Offset == TimeSpan.Zero
            ? lastWriteTimeUtcHint
            : throw new ArgumentException(
                "Last-write time hints must be normalized to UTC.",
                nameof(lastWriteTimeUtcHint));
}

/// <summary>Stable General selected-file inspection problem.</summary>
public sealed record GeneralSelectedFileInspectionIssue(
    string Code,
    string Message,
    string DefinitionId);

/// <summary>Closed result for one selected General file.</summary>
public sealed record GeneralSelectedFileInspectionResult(
    GeneralSelectedFileInspection? Inspection,
    GeneralSelectedFileInspectionIssue? Issue)
{
    /// <summary>True only when one content-authoritative inspection was captured.</summary>
    public bool Succeeded => Inspection is not null && Issue is null;
}

/// <summary>Stable issue codes shared by desktop and CLI adapters.</summary>
public static class GeneralSelectedFileInspectionIssueCodes
{
    /// <summary>The selected file could not be read and content-identified.</summary>
    public const string InspectionFailed =
        "authoring.general.selected-file-inspection-failed";

    /// <summary>Validation or compilation received an uninspected selected file.</summary>
    public const string SnapshotRequired =
        "authoring.general.selected-file-snapshot-required";

}

/// <summary>
/// Application-owned inspection policy for selected General mapping files.
/// </summary>
public sealed class GeneralSelectedFileInspectionService
{
    private readonly ISelectedFileContentInspector _inspector;
    private readonly long _maximumFileBytes;

    /// <summary>Creates the shared Application use case over one host adapter.</summary>
    public GeneralSelectedFileInspectionService(
        ISelectedFileContentInspector inspector,
        long maximumFileBytes = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileBytes);
        _inspector = inspector;
        _maximumFileBytes = maximumFileBytes;
    }

    /// <summary>Inspects one selected mapping file through the shared content-authoritative port.</summary>
    public async ValueTask<GeneralSelectedFileInspectionResult> InspectAsync(
        string definitionId,
        string selectedPath,
        AuthoringRevision authoringRevision,
        CancellationToken cancellationToken)
    {
        List<GeneralSelectedFileInspectionIssue> issues = [];
        GeneralSelectedFileInspection? inspection = await TryInspectAsync(
            definitionId,
            authoringRevision,
            selectedPath,
            issues,
            cancellationToken).ConfigureAwait(false);
        return new GeneralSelectedFileInspectionResult(
            inspection,
            issues.SingleOrDefault());
    }

    private async ValueTask<GeneralSelectedFileInspection?> TryInspectAsync(
        string definitionId,
        AuthoringRevision authoringRevision,
        string selectedPath,
        List<GeneralSelectedFileInspectionIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            SelectedFileContentInspection inspected =
                await _inspector.InspectAsync(
                        selectedPath,
                        _maximumFileBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            return new GeneralSelectedFileInspection(
                definitionId,
                authoringRevision,
                selectedPath,
                inspected.FileStamp,
                inspected.DisplayNameHint,
                inspected.LastWriteTimeUtcHint,
                inspected.AcceptedBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SelectedFileSizeLimitExceededException exception)
        {
            issues.Add(new GeneralSelectedFileInspectionIssue(
                GeneralAuthoringIssueCodes.FileSizeExceeded,
                $"Selected General file is {exception.ObservedBytes} bytes, exceeding the resolved whole-file maximum {exception.MaximumBytes}.",
                definitionId));
            return null;
        }
        catch (Exception exception)
        {
            issues.Add(new GeneralSelectedFileInspectionIssue(
                GeneralSelectedFileInspectionIssueCodes.InspectionFailed,
                $"Selected General file inspection failed ({exception.GetType().Name}).",
                definitionId));
            return null;
        }
    }
}
