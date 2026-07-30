using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    internal static bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(icId, dpInputLength, out composition, out issues) &&
            composition is not null;
    }

    internal static bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string> selectedInputSlotIds,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedInputSlotIds);
        composition = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(
                icId,
                dpInputLength,
                selectedInputSlotIds,
                out composition,
                out issues) &&
            composition is not null;
    }
}
