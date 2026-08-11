using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    internal bool TryCompileStandardMerge(
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

    internal bool TryCompileStandardMerge(
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

    internal bool TryCompileStandardMerge(
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
