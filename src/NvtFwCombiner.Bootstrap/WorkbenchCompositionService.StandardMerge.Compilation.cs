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
        return TryCompileStandardMerge(
            icId,
            dpInputLength,
            includeOptionalLdc: true,
            out composition,
            out issues);
    }

    internal static bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        bool includeOptionalLdc,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(
                icId,
                dpInputLength,
                includeOptionalLdc,
                out composition,
                out issues) &&
            composition is not null;
    }
}
