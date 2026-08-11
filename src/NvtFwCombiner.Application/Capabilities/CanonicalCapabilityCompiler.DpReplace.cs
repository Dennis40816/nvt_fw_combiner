using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    internal bool TryCompileDpReplace(
        string icId,
        long baseCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileDpReplace(
            icId,
            baseCapacity,
            selectedInputSlotIds: null,
            out composition,
            out _,
            out issues);
    }

    internal bool TryCompileDpReplace(
        string icId,
        long baseCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileDpReplace(
            icId,
            baseCapacity,
            selectedInputSlotIds,
            out composition,
            out _,
            out issues);
    }

    internal bool TryCompileDpReplace(
        string icId,
        long baseCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompilePublishedDynamicCapability(
                icId,
                ExperienceIds.DpReplace,
                "1-ic",
                baseCapacity,
                selectedInputSlotIds,
                out composition,
                out resolvedCapability,
                out issues) ||
            TryCompilePublishedDpReplaceCapability(
                icId,
                baseCapacity,
                out composition,
                out resolvedCapability,
                out issues);
    }
}
