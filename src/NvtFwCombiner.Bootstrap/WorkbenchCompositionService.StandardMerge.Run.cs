using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Runs Standard Merge preview or build through the application core.</summary>
    public static async ValueTask<WorkbenchRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);

        if (FindStandardMergeProfileSummaryByIc(icId) is null)
        {
            throw new InvalidOperationException($"Standard Merge is not available for '{icId}'.");
        }

        long? dpInputLength = GetExistingStandardMergeDpInputLength(slotPaths);
        if (!TryCompileStandardMerge(icId, dpInputLength, out CompiledComposition? compiledComposition, out IReadOnlyList<CompositionIssue> issues))
        {
            throw new InvalidOperationException(FormatIssues(issues));
        }

        CompositionPlan plan = compiledComposition.Plan;
        InputArtifactBinding[] bindings = [
            .. plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => CreateBinding(compiledComposition, addressSpaceId, slotPaths)),
        ];

        return await RunCompiledCompositionAsync(
            StandardMergeRunIdPrefix,
            compiledComposition,
            bindings,
            bindings[0].ArtifactId,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: null,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
    }

    private static long? GetExistingStandardMergeDpInputLength(IReadOnlyDictionary<string, string> slotPaths)
    {
        return !slotPaths.TryGetValue(CompositionAddressSpaceIds.DpInput, out string? dpPath) ||
            string.IsNullOrWhiteSpace(dpPath) ||
            !File.Exists(dpPath)
                ? null
                : new FileInfo(dpPath).Length;
    }
}
