using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Owns typed General Merge initializer and draft authoring.</summary>
public static class GeneralMergeAuthoringUseCase
{
    /// <summary>Formats one profile-owned output length for authoring.</summary>
    public static string FormatDefaultOutputLength(
        GeneralMergeOutputInitializer initializer)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        return AuthoringByteRangeCodec.FormatHex(initializer.Capacity);
    }

    /// <summary>Formats one profile-owned output fill byte for authoring.</summary>
    public static string FormatDefaultOutputFillByte(
        GeneralMergeOutputInitializer initializer)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        return $"0x{initializer.FillByte:X2}";
    }

    /// <summary>Resolves typed General Merge initializer text.</summary>
    public static bool TryResolveOutputInitializer(
        string? outputLength,
        string? outputFillByte,
        [NotNullWhen(true)] out GeneralMergeInitializer? initializer)
    {
        GeneralMergeInitializerResolution result = ResolveOutputInitializer(outputLength, outputFillByte);
        initializer = result.Initializer;
        return initializer is not null && result.Issue is null;
    }

    /// <summary>Resolves initializer text without discarding the canonical refusal reason.</summary>
    public static GeneralMergeInitializerResolution ResolveOutputInitializer(
        string? outputLength,
        string? outputFillByte)
    {
        bool resolved = new GeneralMergeInitializerInput(
            outputLength,
            outputFillByte).TryResolve(
                out GeneralMergeOutputInitializer? value,
                out CompositionIssue? issue);
        return new GeneralMergeInitializerResolution(
            resolved ? new GeneralMergeInitializer(value!) : null,
            issue);
    }

    /// <summary>Prepares a draft while retaining initializer and mapping refusals.</summary>
    public static GeneralMergeDraftPreparation PrepareDraft(
        string? outputLength,
        string? outputFillByte,
        IReadOnlyList<AuthoringMappingState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        GeneralMergeInitializerResolution initializer = ResolveOutputInitializer(outputLength, outputFillByte);
        bool mappingsResolved = GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft(
            states,
            out GeneralMappingDraftState? mappings,
            out IReadOnlyList<CompositionIssue> draftIssues);
        return new GeneralMergeDraftPreparation(
            initializer,
            initializer.Succeeded && mappingsResolved
                ? CreateDraft(initializer.Initializer!, mappings!)
                : null,
            draftIssues);
    }

    /// <summary>Gets the deterministic default output filename.</summary>
    public static string GetDefaultOutputFileName(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return $"{icId.ToLowerInvariant()}-general-merge.bin";
    }

    /// <summary>Creates one typed General Merge draft.</summary>
    public static GeneralMergeDraftState CreateDraft(
        GeneralMergeInitializer initializer,
        GeneralMappingDraftState mappings)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(mappings);
        return new GeneralMergeDraftState(initializer.Value, mappings);
    }
}
