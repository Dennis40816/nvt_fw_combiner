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
        bool resolved = new GeneralMergeInitializerInput(
            outputLength,
            outputFillByte).TryResolve(
                out GeneralMergeOutputInitializer? value,
                out _);
        initializer = resolved
            ? new GeneralMergeInitializer(value!)
            : null;
        return resolved;
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
