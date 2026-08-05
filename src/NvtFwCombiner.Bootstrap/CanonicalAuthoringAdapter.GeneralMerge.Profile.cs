using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CanonicalAuthoringAdapter
{
    /// <summary>Gets the default General Merge output length text for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputLength(string icId)
    {
        return BootstrapRangeText.FormatHex(
            GetGeneralMergeDefaultOutputInitializer(icId).Capacity);
    }

    /// <summary>Gets the default General Merge fill-byte text for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputFillByte(string icId)
    {
        return $"0x{GetGeneralMergeDefaultOutputInitializer(icId).FillByte:X2}";
    }

    /// <summary>Gets the profile-derived default typed General Merge initializer.</summary>
    public static GeneralMergeOutputInitializer GetGeneralMergeDefaultOutputInitializer(
        string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        _ = CanonicalCapabilityProjection.FindStandardMergeProfileSummary(icId) ??
            throw new InvalidOperationException(
            $"No compiled V2 Standard Merge profile is registered for '{icId}'.");

        return CanonicalCapabilityResolution.TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
                    icId,
                    out long capacitySelectionDefault,
                    out IReadOnlyList<CompositionIssue> capacityIssues)
            ? new GeneralMergeOutputInitializer(capacitySelectionDefault)
            : capacityIssues.Count != 0
                ? throw new InvalidOperationException(
                    CompositionExecutionAdapter.FormatIssues(capacityIssues))
                : CanonicalCapabilityResolution.TryCompileStandardMerge(
                        icId,
                        dpInputLength: null,
                        out CompiledComposition? composition,
                        out IReadOnlyList<CompositionIssue> issues)
                    ? new GeneralMergeOutputInitializer(
                        composition.Plan.OutputInitialization.Capacity)
                    : throw new InvalidOperationException(
                    CompositionExecutionAdapter.FormatIssues(issues));
    }

    /// <summary>Resolves editable UI text into one typed workbench initializer.</summary>
    public static bool TryResolveGeneralMergeOutputInitializer(
        string? outputLength,
        string? outputFillByte,
        [NotNullWhen(true)] out WorkbenchGeneralMergeInitializer? initializer)
    {
        bool resolved = TryResolveGeneralMergeInitializer(
            outputLength,
            outputFillByte,
            out GeneralMergeOutputInitializer? value,
            out _);
        initializer = resolved
            ? new WorkbenchGeneralMergeInitializer(value!)
            : null;
        return resolved;
    }

    /// <summary>Gets the profile-owned default General Merge output file name for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputFileName(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge.bin";
    }

    internal static bool TryResolveGeneralMergeInitializer(
        string? outputLength,
        string? outputFillByte,
        out GeneralMergeOutputInitializer? initializer,
        out CompositionIssue? issue)
    {
        return new GeneralMergeInitializerInput(
            outputLength,
            outputFillByte).TryResolve(
                out initializer,
            out issue);
    }

    /// <summary>Combines a resolved initializer and typed mappings without exposing Domain construction to Presentation.</summary>
    public static GeneralMergeDraftState CreateGeneralMergeDraft(
        WorkbenchGeneralMergeInitializer initializer,
        GeneralMappingDraftState mappings)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(mappings);
        return new GeneralMergeDraftState(initializer.Value, mappings);
    }
}
