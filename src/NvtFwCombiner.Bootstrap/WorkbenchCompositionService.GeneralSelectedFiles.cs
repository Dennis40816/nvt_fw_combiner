using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>
    /// Both desktop/workbench and CLI paths cross this Application result
    /// before General validation, compilation, Preview, Build, or reports.
    /// </summary>
    private static async ValueTask<GeneralSelectedFileBindingResult>
        AcceptGeneralSelectedFilesAsync(
            GeneralMappingDraftState draft,
            AuthoringRevision authoringRevision,
            CancellationToken cancellationToken)
    {
        GeneralMappingDraftRow[] pendingRows =
        [
            .. draft.Rows.Where(static row =>
                row.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
                row.Source.AcceptedFileStamp is null),
        ];
        if (pendingRows.Length == 0)
        {
            return new GeneralSelectedFileBindingResult(draft, []);
        }

        string[] roots =
        [
            .. pendingRows
                .Select(static row =>
                    Path.GetDirectoryName(
                        Path.GetFullPath(row.Source.Reference))!)
                .Distinct(
                    OperatingSystem.IsWindows()
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal),
        ];
        var service = new GeneralSelectedFileInspectionService(
            new FileContentSnapshotInspector(roots));
        GeneralSelectedFileDraftInspectionResult result =
            await service.AcceptDraftAsync(
                draft,
                authoringRevision,
                cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded
            ? new GeneralSelectedFileBindingResult(result.Draft!, [])
            : new GeneralSelectedFileBindingResult(
                Draft: null,
                [
                    .. result.Issues.Select(static issue =>
                        new CompositionIssue(
                            issue.Code,
                            issue.Message,
                            issue.DefinitionId)),
                ]);
    }

    private static GeneralSelectedFileBindingResult RequireAcceptedGeneralSelectedFiles(
        GeneralMappingDraftState draft)
    {
        CompositionIssue[] issues =
        [
            .. draft.Rows
                .Where(static row =>
                    row.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
                    row.Source.AcceptedFileStamp is null)
                .Select(static row => new CompositionIssue(
                    GeneralSelectedFileInspectionIssueCodes.SnapshotRequired,
                    "The selected General file requires explicit inspection or Reload/Rebind before execution.",
                    row.MappingId)),
        ];
        return issues.Length == 0
            ? new GeneralSelectedFileBindingResult(draft, [])
            : new GeneralSelectedFileBindingResult(Draft: null, issues);
    }

    private static bool IsAcceptedGeneralMappingDraft(
        GeneralMappingDraftState? draft)
    {
        return draft is not null &&
            draft.Rows.All(static row =>
                row.Source.Kind != GeneralMappingSourceKind.FileArtifact ||
                row.Source.AcceptedFileStamp is not null);
    }

    private sealed record GeneralSelectedFileBindingResult(
        GeneralMappingDraftState? Draft,
        IReadOnlyList<CompositionIssue> Issues)
    {
        internal bool Succeeded => Draft is not null && Issues.Count == 0;
    }
}
