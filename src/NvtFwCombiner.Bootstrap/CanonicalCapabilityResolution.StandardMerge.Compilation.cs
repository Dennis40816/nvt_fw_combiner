using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class CanonicalCapabilityResolution
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
        return TryCompileStandardMerge(
            icId,
            dpInputLength,
            selectedInputSlotIds,
            out composition,
            out _,
            out issues);
    }

    internal static bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string> selectedInputSlotIds,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedInputSlotIds);
        composition = null;
        resolvedCapability = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(
                icId,
                dpInputLength,
                selectedInputSlotIds,
                out composition,
                out resolvedCapability,
                out issues) &&
            composition is not null;
    }
}
