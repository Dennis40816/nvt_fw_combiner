using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Runs Standard Merge preview or build through the application core.</summary>
    public static ValueTask<WorkbenchRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return RunStandardMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            progress: null,
            cancellationToken);
    }

    /// <summary>Runs Standard Merge and publishes bounded Application-owned lifecycle phases.</summary>
    public static ValueTask<WorkbenchRunResult> RunStandardMergeWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return RunStandardMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            progress,
            cancellationToken);
    }

    private static async ValueTask<WorkbenchRunResult> RunStandardMergeCoreAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);

        if (FindStandardMergeProfileSummaryByIc(icId) is null)
        {
            throw new InvalidOperationException($"Standard Merge is not available for '{icId}'.");
        }

        if (!TryGetStandardMergeDpInputLength(icId, slotPaths, out long? dpInputLength, out CompositionIssue? inputIssue))
        {
            throw new InvalidOperationException(FormatIssues([inputIssue]));
        }

        if (!TryCompileStandardMerge(icId, dpInputLength, out CompiledComposition? compiledComposition, out IReadOnlyList<CompositionIssue> issues))
        {
            throw new InvalidOperationException(FormatIssues(issues));
        }

        CompositionPlan plan = compiledComposition.Plan;
        InputArtifactBinding[] bindings = [
            .. plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => slotPaths.TryGetValue(addressSpaceId, out string? path) &&
                    !string.IsNullOrWhiteSpace(path)
                        ? CompiledCompositionInputBindingFactory.Create(
                            compiledComposition,
                            addressSpaceId,
                            Path.GetFullPath(path))
                        : throw new InvalidOperationException($"Input slot '{addressSpaceId}' is required.")),
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
            cancellationToken,
            progress: progress).ConfigureAwait(false);
    }

    private static bool TryGetStandardMergeDpInputLength(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        out long? dpInputLength,
        [NotNullWhen(false)] out CompositionIssue? issue)
    {
        _ = slotPaths.TryGetValue(CompositionAddressSpaceIds.DpInput, out string? dpPath);
        return TryGetStandardMergeDpInputLength(icId, dpPath, out dpInputLength, out issue);
    }

    internal static bool TryGetStandardMergeDpInputLength(
        string icId,
        string? dpPath,
        out long? dpInputLength,
        [NotNullWhen(false)] out CompositionIssue? issue)
    {
        dpInputLength = null;
        issue = null;
        if (!IsBuiltInV2StandardMergeMapCapacityPending(icId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(dpPath) || !File.Exists(dpPath))
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.InputArtifactReadFailed,
                $"Selected DP BIN path does not exist for {icId} Standard Merge.",
                CompositionAddressSpaceIds.DpInput);
            return false;
        }

        dpInputLength = new FileInfo(dpPath).Length;
        return true;
    }
}
