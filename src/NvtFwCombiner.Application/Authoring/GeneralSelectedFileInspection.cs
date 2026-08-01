using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// One immutable Application result shared by General authoring consumers.
/// Definition and revision guard publication; <see cref="FileStamp"/> alone
/// identifies the accepted bytes.
/// </summary>
public sealed class GeneralSelectedFileInspection
{
    /// <summary>Creates one accepted General selected-file result.</summary>
    public GeneralSelectedFileInspection(
        string definitionId,
        AuthoringRevision authoringRevision,
        string selectedPathHint,
        FileStamp fileStamp,
        string? displayNameHint = null,
        DateTimeOffset? lastWriteTimeUtcHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPathHint);
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

        DefinitionId = definitionId;
        AuthoringRevision = authoringRevision;
        SelectedPathHint = selectedPathHint;
        FileStamp = fileStamp;
        DisplayNameHint = displayNameHint;
        LastWriteTimeUtcHint = lastWriteTimeUtcHint;
    }

    /// <summary>Resolved slot or mapping definition inspected.</summary>
    public string DefinitionId { get; }

    /// <summary>Authoring revision for which the inspection was requested.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Non-authoritative selected-path hint used for stale-result rejection.</summary>
    public string SelectedPathHint { get; }

    /// <summary>Accepted length and SHA-256 content identity.</summary>
    public FileStamp FileStamp { get; }

    /// <summary>Non-authoritative display-name hint.</summary>
    public string? DisplayNameHint { get; }

    /// <summary>Non-authoritative filesystem timestamp hint.</summary>
    public DateTimeOffset? LastWriteTimeUtcHint { get; }
}

/// <summary>Stable General selected-file inspection problem.</summary>
public sealed record GeneralSelectedFileInspectionIssue(
    string Code,
    string Message,
    string DefinitionId);

/// <summary>Stable issue codes shared by desktop and CLI adapters.</summary>
public static class GeneralSelectedFileInspectionIssueCodes
{
    /// <summary>The selected file could not be read and content-identified.</summary>
    public const string InspectionFailed =
        "authoring.general.selected-file-inspection-failed";

    /// <summary>The requested mapping definition is absent or is not file-backed.</summary>
    public const string DefinitionUnavailable =
        "authoring.general.selected-file-definition-unavailable";

    /// <summary>Validation or compilation received an uninspected selected file.</summary>
    public const string SnapshotRequired =
        "authoring.general.selected-file-snapshot-required";
}

/// <summary>
/// Immutable result of accepting or explicitly reloading selected files in one
/// General mapping draft.
/// </summary>
public sealed class GeneralSelectedFileDraftInspectionResult
{
    private readonly GeneralSelectedFileInspection[] _inspections;
    private readonly GeneralSelectedFileInspectionIssue[] _issues;

    internal GeneralSelectedFileDraftInspectionResult(
        AuthoringRevision authoringRevision,
        GeneralMappingDraftState? draft,
        IEnumerable<GeneralSelectedFileInspection> inspections,
        IEnumerable<GeneralSelectedFileInspectionIssue> issues)
    {
        AuthoringRevision = authoringRevision;
        Draft = draft;
        _inspections = [.. inspections];
        _issues = [.. issues];
        Inspections = Array.AsReadOnly(_inspections);
        Issues = Array.AsReadOnly(_issues);
    }

    /// <summary>Revision bound to every accepted result.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Content-bound immutable draft, or null after failure.</summary>
    public GeneralMappingDraftState? Draft { get; }

    /// <summary>New immutable inspection results shared by all consumers.</summary>
    public IReadOnlyList<GeneralSelectedFileInspection> Inspections { get; }

    /// <summary>Stable inspection problems.</summary>
    public IReadOnlyList<GeneralSelectedFileInspectionIssue> Issues { get; }

    /// <summary>True only when a content-bound draft is available.</summary>
    public bool Succeeded => Draft is not null && Issues.Count == 0;
}

/// <summary>
/// Application-owned inspection and explicit reload policy for selected
/// General mapping files.
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
        List<GeneralSelectedFileInspection> inspections = [];
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

            inspections.Add(inspection);
            rows.Add(row.WithAcceptedFileStamp(inspection.FileStamp));
        }

        return issues.Count == 0
            ? new GeneralSelectedFileDraftInspectionResult(
                authoringRevision,
                new GeneralMappingDraftState(rows),
                inspections,
                [])
            : new GeneralSelectedFileDraftInspectionResult(
                authoringRevision,
                draft: null,
                inspections,
                issues);
    }

    /// <summary>
    /// Explicitly reloads one mapping file, advances revision, accepts a fresh
    /// stamp, and otherwise preserves the editable row.
    /// </summary>
    public async ValueTask<GeneralSelectedFileDraftInspectionResult> ReloadAsync(
        GeneralMappingDraftState draft,
        string definitionId,
        AuthoringRevision currentRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        AuthoringRevision nextRevision = currentRevision.Next();
        GeneralMappingDraftRow? selected = draft.Rows.SingleOrDefault(row =>
            StringComparer.Ordinal.Equals(row.MappingId, definitionId) &&
            row.Source.Kind == GeneralMappingSourceKind.FileArtifact);
        if (selected is null)
        {
            return new GeneralSelectedFileDraftInspectionResult(
                nextRevision,
                draft: null,
                [],
                [
                    new GeneralSelectedFileInspectionIssue(
                        GeneralSelectedFileInspectionIssueCodes.DefinitionUnavailable,
                        "The requested General mapping does not bind a selected file.",
                        definitionId),
                ]);
        }

        List<GeneralSelectedFileInspectionIssue> issues = [];
        GeneralSelectedFileInspection? inspection =
            await TryInspectAsync(
                definitionId,
                nextRevision,
                selected.Source.Reference,
                issues,
                cancellationToken)
            .ConfigureAwait(false);
        if (inspection is null)
        {
            return new GeneralSelectedFileDraftInspectionResult(
                nextRevision,
                draft: null,
                [],
                issues);
        }

        GeneralMappingDraftRow[] rows =
        [
            .. draft.Rows.Select(row =>
                ReferenceEquals(row, selected)
                    ? row.WithAcceptedFileStamp(inspection.FileStamp)
                    : row),
        ];
        return new GeneralSelectedFileDraftInspectionResult(
            nextRevision,
            new GeneralMappingDraftState(rows),
            [inspection],
            []);
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
