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
        return GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
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
        return GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
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
        if (!GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(
                outputLength,
                outputFillByte,
                out GeneralMergeInitializer? initializer))
        {
            throw new InvalidOperationException("Test input must contain a valid General Merge initializer.");
        }

        GeneralMappingDraftState mappingDraft = GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft(
                mappings,
                out GeneralMappingDraftState? draft,
                out IReadOnlyList<CompositionIssue> issues)
            ? draft
            : throw new InvalidOperationException(
                string.Join("; ", issues.Select(static issue => issue.Message)));

        return GeneralMergeAuthoringUseCase.CreateDraft(initializer, mappingDraft);
    }

    internal static GeneralMappingDraftState CreateReplaceDraft(
        IReadOnlyList<AuthoringMappingState> mappings)
    {
        return GeneralAuthoringMappingUseCase.TryCreateGeneralReplaceAuthoringDraft(
            mappings,
            out GeneralMappingDraftState? draft,
            out IReadOnlyList<CompositionIssue> issues)
                ? draft
                : throw new InvalidOperationException(
                    string.Join("; ", issues.Select(static issue => issue.Message)));
    }

}
