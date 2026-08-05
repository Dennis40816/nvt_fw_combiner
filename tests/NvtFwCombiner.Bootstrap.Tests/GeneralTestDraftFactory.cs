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
        return CanonicalAuthoringAdapter.CreateGeneralReplaceAuthoringState(
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
        return CanonicalAuthoringAdapter.CreateGeneralReplaceAuthoringState(
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
        if (!CanonicalAuthoringAdapter.TryResolveGeneralMergeOutputInitializer(
                outputLength,
                outputFillByte,
                out WorkbenchGeneralMergeInitializer? initializer))
        {
            throw new InvalidOperationException("Test input must contain a valid General Merge initializer.");
        }

        GeneralMappingDraftState mappingDraft = CanonicalAuthoringAdapter.TryCreateGeneralMergeAuthoringDraft(
                mappings,
                out GeneralMappingDraftState? draft,
                out IReadOnlyList<CompositionIssue> issues)
            ? draft
            : throw new InvalidOperationException(
                string.Join("; ", issues.Select(static issue => issue.Message)));

        return CanonicalAuthoringAdapter.CreateGeneralMergeDraft(initializer, mappingDraft);
    }

    internal static GeneralMappingDraftState CreateReplaceDraft(
        IReadOnlyList<AuthoringMappingState> mappings)
    {
        return CanonicalAuthoringAdapter.TryCreateGeneralReplaceAuthoringDraft(
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
        return !CanonicalAuthoringAdapter.TryResolveGeneralMergeOutputInitializer(
                outputLength,
                outputFillByte,
                out WorkbenchGeneralMergeInitializer? initializer)
            ? CompositionMemoryProjection.GetGeneralMergeMemoryDisplay(
                icId,
                outputLength,
                outputFillByte)
            : CanonicalAuthoringAdapter.TryCreateGeneralMergeAuthoringDraft(
                mappings,
                out GeneralMappingDraftState? mappingDraft,
                out _)
                ? CompositionMemoryProjection.GetGeneralMergeMemoryDisplay(
                    icId,
                    CanonicalAuthoringAdapter.CreateGeneralMergeDraft(initializer, mappingDraft))
                : CompositionMemoryProjection.GetGeneralMergeMemoryDisplay(
                    icId,
                    initializer,
                    mappings,
                    admission: null);
    }
}
