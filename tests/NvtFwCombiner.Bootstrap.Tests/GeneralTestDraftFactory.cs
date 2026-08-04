using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Creates canonical typed General drafts for focused test scenarios.</summary>
internal static class GeneralTestDraftFactory
{
    internal static AuthoringMappingState ReplaceFile(
        string id,
        string path,
        string targetStart,
        string length)
    {
        return WorkbenchCompositionService.CreateGeneralReplaceAuthoringState(
            id,
            GeneralMappingSourceKind.FileArtifact,
            path,
            targetStart,
            length);
    }

    internal static AuthoringMappingState ReplacePatch(
        string id,
        string targetStart,
        string length,
        GeneralMappingSourceKind kind,
        string value)
    {
        return WorkbenchCompositionService.CreateGeneralReplaceAuthoringState(
            id,
            kind,
            value,
            targetStart,
            length);
    }

    internal static GeneralMergeDraftState CreateMergeDraft(
        string outputLength,
        IReadOnlyList<AuthoringMappingState> mappings,
        string? outputFillByte = null)
    {
        if (!WorkbenchCompositionService.TryResolveGeneralMergeOutputInitializer(
                outputLength,
                outputFillByte,
                out WorkbenchGeneralMergeInitializer? initializer))
        {
            throw new InvalidOperationException("Test input must contain a valid General Merge initializer.");
        }

        GeneralMappingDraftState mappingDraft = WorkbenchCompositionService.TryCreateGeneralMergeAuthoringDraft(
                mappings,
                out GeneralMappingDraftState? draft,
                out IReadOnlyList<CompositionIssue> issues)
            ? draft
            : throw new InvalidOperationException(
                string.Join("; ", issues.Select(static issue => issue.Message)));

        return WorkbenchCompositionService.CreateGeneralMergeDraft(initializer, mappingDraft);
    }

    internal static GeneralMappingDraftState CreateReplaceDraft(
        IReadOnlyList<AuthoringMappingState> mappings)
    {
        return WorkbenchCompositionService.TryCreateGeneralReplaceAuthoringDraft(
            mappings,
            out GeneralMappingDraftState? draft,
            out IReadOnlyList<CompositionIssue> issues)
                ? draft
                : throw new InvalidOperationException(
                    string.Join("; ", issues.Select(static issue => issue.Message)));
    }

    internal static WorkbenchMemoryDisplay GetMergeDisplay(
        string icId,
        string outputLength,
        IReadOnlyList<AuthoringMappingState> mappings,
        string? outputFillByte = null)
    {
        return !WorkbenchCompositionService.TryResolveGeneralMergeOutputInitializer(
                outputLength,
                outputFillByte,
                out WorkbenchGeneralMergeInitializer? initializer)
            ? WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(
                icId,
                outputLength,
                outputFillByte)
            : WorkbenchCompositionService.TryCreateGeneralMergeAuthoringDraft(
                mappings,
                out GeneralMappingDraftState? mappingDraft,
                out _)
                ? WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(
                    icId,
                    WorkbenchCompositionService.CreateGeneralMergeDraft(initializer, mappingDraft))
                : WorkbenchCompositionService.GetGeneralMergeMemoryDisplay(
                    icId,
                    initializer,
                    mappings,
                    admission: null);
    }
}
