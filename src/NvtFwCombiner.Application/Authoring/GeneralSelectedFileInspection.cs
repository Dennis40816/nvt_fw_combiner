using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// One immutable Application result shared by General authoring consumers.
/// Definition and revision guard publication; <see cref="FileStamp"/> alone
/// identifies the accepted bytes.
/// </summary>
public sealed class GeneralSelectedFileInspection(
    string definitionId,
    AuthoringRevision authoringRevision,
    string selectedPathHint,
    FileStamp fileStamp,
    string? displayNameHint = null,
    DateTimeOffset? lastWriteTimeUtcHint = null)
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

/// <summary>Closed pre-binding length observation for one selected General file.</summary>
public sealed record GeneralSelectedFileLengthResult(
    long? ObservedLength,
    GeneralSelectedFileInspectionIssue? Issue)
{
    /// <summary>True only when a bounded positive length is available for exact compilation.</summary>
    public bool Succeeded => ObservedLength > 0 && Issue is null;
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

    /// <summary>The file length changed after exact candidate compilation.</summary>
    public const string ObservedLengthChanged =
        "authoring.general.selected-file-length-changed";
}

/// <summary>
/// Immutable result of accepting selected files in one General mapping draft.
/// </summary>
public sealed class GeneralSelectedFileDraftInspectionResult
{
    internal GeneralSelectedFileDraftInspectionResult(
        GeneralMappingDraftState? draft,
        IEnumerable<GeneralSelectedFileInspectionIssue> issues)
    {
        Draft = draft;
        Issues = Array.AsReadOnly([.. issues]);
    }

    /// <summary>Content-bound immutable draft, or null after failure.</summary>
    public GeneralMappingDraftState? Draft { get; }

    /// <summary>Stable inspection problems.</summary>
    public IReadOnlyList<GeneralSelectedFileInspectionIssue> Issues { get; }

    /// <summary>True only when a content-bound draft is available.</summary>
    public bool Succeeded => Draft is not null && Issues.Count == 0;
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

    /// <summary>Observes one bounded length before the same route compiles its exact candidate input contract.</summary>
    public async ValueTask<GeneralSelectedFileLengthResult> ObserveLengthAsync(
        string definitionId,
        string selectedPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        try
        {
            long length = await _inspector.ObserveLengthAsync(
                selectedPath,
                _maximumFileBytes,
                cancellationToken).ConfigureAwait(false);
            return length > 0
                ? new GeneralSelectedFileLengthResult(length, null)
                : Failed(GeneralAuthoringIssueCodes.SlotLengthRejected,
                    "Selected General files must be non-empty.", definitionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SelectedFileSizeLimitExceededException exception)
        {
            return Failed(GeneralAuthoringIssueCodes.FileSizeExceeded,
                $"Selected General file is {exception.ObservedBytes} bytes, exceeding the resolved whole-file maximum {exception.MaximumBytes}.", definitionId);
        }
        catch (Exception exception)
        {
            return Failed(GeneralSelectedFileInspectionIssueCodes.InspectionFailed,
                $"Selected General file inspection failed ({exception.GetType().Name}).", definitionId);
        }

        static GeneralSelectedFileLengthResult Failed(
            string code,
            string message,
            string id)
        {
            return new(null, new GeneralSelectedFileInspectionIssue(code, message, id));
        }
    }

    /// <summary>
    /// Inspects every unbound selected General file once for this draft and
    /// revision. Already accepted sources are not silently rebound.
    /// </summary>
    public async ValueTask<GeneralSelectedFileDraftInspectionResult> AcceptDraftAsync(
        GeneralMappingDraftState draft,
        AuthoringRevision authoringRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        List<GeneralMappingDraftRow> rows = [];
        List<GeneralSelectedFileInspectionIssue> issues = [];
        foreach (GeneralMappingDraftRow row in draft.Rows)
        {
            if (row.Source.Kind != GeneralMappingSourceKind.FileArtifact ||
                row.Source.AcceptedFileStamp is not null)
            {
                rows.Add(row);
                continue;
            }

            GeneralSelectedFileInspection? inspection =
                await TryInspectAsync(
                    row.MappingId,
                    authoringRevision,
                    row.Source.Reference,
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
            if (inspection is null)
            {
                rows.Add(row);
                continue;
            }

            rows.Add(row.WithAcceptedFileStamp(inspection.FileStamp));
        }

        return new GeneralSelectedFileDraftInspectionResult(
            issues.Count == 0 ? new GeneralMappingDraftState(rows) : null,
            issues);
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
                inspected.LastWriteTimeUtcHint);
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
