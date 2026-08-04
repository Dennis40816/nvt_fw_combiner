using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Projects editable workbench General Merge text through the Application authoring seam.</summary>
    public static AuthoringMappingState CreateGeneralMergeAuthoringState(
        string mappingId,
        string filePath,
        string sourceStart,
        string targetStart,
        string length,
        int alignment = 1,
        string? reason = null,
        OperationProvenance? provenance = null,
        FileStamp? acceptedFileStamp = null)
    {
        return AuthoringMappingState.Create(
            mappingId,
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File(filePath, acceptedFileStamp),
            sourceStart,
            targetStart,
            length,
            CompositionAddressSpaceIds.OutputImage,
            OverlapPolicy.Reject,
            alignment,
            reason ?? "Copy explicit General Merge mapping.",
            WorkbenchGeneralMergeIds.OutputRegionId,
            provenance,
            GeneralMappingFileRangePreset.SourceSlice);
    }

    /// <summary>Projects one canonical file, overwrite, or fill source into General Replace authoring.</summary>
    public static AuthoringMappingState CreateGeneralReplaceAuthoringState(
        string mappingId,
        GeneralMappingSourceKind sourceKind,
        string sourceValue,
        string targetStart,
        string length,
        FileStamp? acceptedFileStamp = null)
    {
        GeneralMappingSource source = sourceKind switch
        {
            GeneralMappingSourceKind.FileArtifact => GeneralMappingSource.File(sourceValue, acceptedFileStamp),
            GeneralMappingSourceKind.HexOverwrite => GeneralMappingSource.HexOverwrite(sourceValue, mappingId),
            GeneralMappingSourceKind.HexFill => GeneralMappingSource.HexFill(sourceValue, mappingId),
            _ => throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, null),
        };
        return AuthoringMappingState.Create(
            mappingId,
            ExplicitMappingOperationKind.ReplaceRange,
            source,
            "0x0",
            targetStart,
            length,
            CompositionAddressSpaceIds.OutputImage,
            OverlapPolicy.Reject,
            alignment: 1,
            "Replace explicit General range.",
            fileRangePreset: sourceKind == GeneralMappingSourceKind.FileArtifact
                ? GeneralMappingFileRangePreset.FromFileStart
                : null);
    }

    /// <summary>Creates one typed General Merge draft from parsed authoring states.</summary>
    public static bool TryCreateGeneralMergeAuthoringDraft(
        IReadOnlyList<AuthoringMappingState> states,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCreateGeneralAuthoringDraft(
            states, ExplicitMappingOperationKind.CopyRange, out draft, out issues);
    }

    /// <summary>Creates one typed General Replace draft from parsed authoring states.</summary>
    public static bool TryCreateGeneralReplaceAuthoringDraft(
        IReadOnlyList<AuthoringMappingState> states,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCreateGeneralAuthoringDraft(
            states, ExplicitMappingOperationKind.ReplaceRange, out draft, out issues);
    }

    private static bool TryCreateGeneralAuthoringDraft(
        IReadOnlyList<AuthoringMappingState> states,
        ExplicitMappingOperationKind expectedOperationKind,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        (string issueCode, string issueLabel) =
            expectedOperationKind == ExplicitMappingOperationKind.CopyRange
                ? (WorkbenchIssueCodes.GeneralMergeRangeInvalid, "General Merge mapping")
                : (WorkbenchIssueCodes.GeneralReplaceRangeInvalid, "General Replace mapping");
        List<CompositionIssue> issueList =
        [
            .. states.Where(static state => !state.IsValid).Select(state =>
                new CompositionIssue(
                    issueCode,
                    $"{issueLabel} '{state.MappingId}' is invalid: {state.Issue!.Message}",
                    state.MappingId)),
            .. states.Where(state =>
                    state.IsValid && state.Mapping!.OperationKind != expectedOperationKind)
                .Select(state => new CompositionIssue(
                    issueCode,
                    $"{issueLabel} '{state.MappingId}' must use {expectedOperationKind}.",
                    state.MappingId)),
        ];
        return TryCompleteGeneralMappingDraft(
            [.. states.Where(state =>
                    state.IsValid && state.Mapping!.OperationKind == expectedOperationKind)
                .Select(static state => state.Mapping!)],
            issueList,
            issueCode,
            out draft,
            out issues);
    }

    internal static bool TryCreateGeneralReplaceDraft(
        IEnumerable<GeneralMappingDraftRow> rows,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return TryCompleteGeneralMappingDraft(
            [.. rows],
            [],
            WorkbenchIssueCodes.GeneralReplacePatchIdDuplicate,
            out draft,
            out issues);
    }

    private static bool TryCompleteGeneralMappingDraft(
        List<GeneralMappingDraftRow> rows,
        List<CompositionIssue> issueList,
        string invalidDraftIssueCode,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (issueList.Count == 0)
        {
            try
            {
                draft = new GeneralMappingDraftState(rows);
                issues = [];
                return true;
            }
            catch (ArgumentException exception)
            {
                issueList.Add(new CompositionIssue(
                    invalidDraftIssueCode,
                    exception.Message,
                    "mapping"));
            }
        }

        draft = null;
        issues = issueList;
        return false;
    }

}
