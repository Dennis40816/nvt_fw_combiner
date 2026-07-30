using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
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

        _ = FindStandardMergeProfileSummaryByIc(icId) ?? throw new InvalidOperationException(
            $"No compiled V2 Standard Merge profile is registered for '{icId}'.");

        return TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
                    icId,
                    out long capacitySelectionDefault,
                    out IReadOnlyList<CompositionIssue> capacityIssues)
            ? new GeneralMergeOutputInitializer(capacitySelectionDefault)
            : capacityIssues.Count != 0
                ? throw new InvalidOperationException(
                    FormatIssues(capacityIssues))
                : TryCompileStandardMerge(
                        icId,
                        dpInputLength: null,
                        out CompiledComposition? composition,
                        out IReadOnlyList<CompositionIssue> issues)
                    ? new GeneralMergeOutputInitializer(
                        composition.Plan.OutputInitialization.Capacity)
                    : throw new InvalidOperationException(
                    FormatIssues(issues));
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

    /// <summary>Gets the legacy profile-id alias retained for persisted General Merge saved-rule compatibility.</summary>
    public static string GetGeneralMergeWorkbenchProfileId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge-workbench";
    }

    private static bool TryResolveGeneralMergeInitializer(
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
}

/// <summary>Presentation-safe typed General Merge initializer resolved by the Bootstrap facade.</summary>
public sealed class WorkbenchGeneralMergeInitializer
{
    internal WorkbenchGeneralMergeInitializer(
        GeneralMergeOutputInitializer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>Exact positive output capacity.</summary>
    public long Capacity => Value.Capacity;

    /// <summary>Exact blank-output fill byte.</summary>
    public byte FillByte => Value.FillByte;

    internal GeneralMergeOutputInitializer Value { get; }
}
