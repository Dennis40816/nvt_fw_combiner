using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionExecutionAdapter
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
            acceptedCapability: null,
            acceptedSession: null,
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
            acceptedCapability: null,
            acceptedSession: null,
            cancellationToken);
    }

    /// <summary>Runs Standard Merge from one exact accepted desktop session.</summary>
    public static ValueTask<WorkbenchRunResult> RunStandardMergeAcceptedSessionWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return RunStandardMergeCoreAsync(
            icId, slotPaths, build, outputPath, progress,
            AcceptedAuthoringSessionBinding.RequireCapability(
                acceptedSession,
                Profiles.IcWorkflowIds.StandardMerge,
                icId,
                AuthoringDerivedResultKind.Inspection),
            acceptedSession,
            cancellationToken);
    }

    private static async ValueTask<WorkbenchRunResult> RunStandardMergeCoreAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        ResolvedCapability? acceptedCapability,
        ActiveSessionSnapshot? acceptedSession,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);

        if (CanonicalCapabilityProjection.FindStandardMergeProfileSummary(icId) is null)
        {
            throw new InvalidOperationException($"Standard Merge is not available for '{icId}'.");
        }

        ResolvedCapability? resolvedCapability = acceptedCapability;
        CompiledComposition? compiledComposition = acceptedCapability?.CompiledComposition;
        if (resolvedCapability is null)
        {
            if (!TryGetStandardMergeDpInputLength(
                    icId, slotPaths, out long? dpInputLength, out CompositionIssue? inputIssue))
            {
                throw new InvalidOperationException(
                    FormatIssues([inputIssue]));
            }
            if (!CanonicalCapabilityResolution.TryCompileStandardMerge(
                    icId,
                    dpInputLength,
                [
                    .. slotPaths
                        .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
                        .Select(static pair => pair.Key),
                ],
                out compiledComposition,
                out resolvedCapability,
                out IReadOnlyList<CompositionIssue> issues))
            {
                throw new InvalidOperationException(
                    FormatIssues(issues));
            }
        }

        CompositionPlan plan = compiledComposition!.Plan;
        InputArtifactBinding[] bindings = [
            .. plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => slotPaths.TryGetValue(addressSpaceId, out string? path) &&
                    !string.IsNullOrWhiteSpace(path)
                        ? acceptedSession is null
                            ? CompiledCompositionInputBindingFactory.Create(
                                compiledComposition,
                                addressSpaceId,
                                Path.GetFullPath(path))
                            : AcceptedAuthoringSessionBinding.Create(
                                compiledComposition,
                                addressSpaceId,
                                path,
                                acceptedSession)
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
            cancellationToken,
            progress: progress,
            resolvedCapability: resolvedCapability).ConfigureAwait(false);
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
        if (!CanonicalCapabilityResolution.IsBuiltInV2StandardMergeMapCapacityPending(icId))
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
