using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryGetBuiltInV2StandardMergeCompilation(
            icId,
            dpInputLength,
            out composition,
            out _,
            out issues);
    }

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompilePublishedDynamicCapability(
                icId,
                ExperienceIds.StandardMerge,
                "selector-free",
                dpInputLength,
                selectedInputSlotIds: null,
                out composition,
                out resolvedCapability,
                out issues) ||
            TryCompilePublishedStandardMergeCapability(
                icId,
                dpInputLength,
                out composition,
                out resolvedCapability,
                out issues);
    }

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompilePublishedDynamicCapability(
                icId,
                ExperienceIds.StandardMerge,
                "selector-free",
                dpInputLength,
                selectedInputSlotIds,
                out composition,
                out resolvedCapability,
                out issues) ||
            TryCompilePublishedStandardMergeCapability(
                icId,
                dpInputLength,
                out composition,
                out resolvedCapability,
                out issues);
    }
}
