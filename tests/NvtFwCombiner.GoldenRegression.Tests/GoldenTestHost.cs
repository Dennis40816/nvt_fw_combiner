using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.GoldenRegression.Tests;

internal static class GoldenTestHost
{
    internal static CompositionHostServices Services { get; } =
        CompositionHostServices.Create();

    internal static async ValueTask<CompositionRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        CompiledAuthoringSelectedInput[] inputs =
        [
            .. slotPaths.Select(static pair => new CompiledAuthoringSelectedInput(
                pair.Key,
                pair.Value,
                File.ReadAllBytes(pair.Value))),
        ];
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        CompiledAuthoringSessionPreparation prepared =
            Services.StandardMergeAuthoring.PrepareSession(
                session,
                icId,
                inputs);
        Assert.True(
            prepared.Succeeded,
            string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        return await Services.CompositionExecution
            .ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    prepared.Snapshot!,
                    slotPaths,
                    build,
                    outputPath: outputPath),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<CompositionRunResult> RunDpReplaceAsync(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        Assert.Equal(ExperienceIds.DpReplace, replaceMode, ignoreCase: true);
        CompiledAuthoringSessionPreparation prepared = PrepareDpReplace(icId, slotPaths);
        ActiveSessionSnapshot snapshot = prepared.Succeeded
            ? prepared.Snapshot!
            : throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                PreparationIssues(prepared).Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));

        return await Services.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    snapshot,
                    slotPaths,
                    build,
                    outputPath: outputPath),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static CompiledAuthoringSessionPreparation PrepareDpReplace(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        CompiledAuthoringSelectionSnapshot discovery =
            Services.DpReplaceAuthoring.GetAuthoringSnapshot(
                icId,
                [],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        CompiledAuthoringInputBinding replacementBinding = discovery.InputBindings.Single(binding =>
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase) &&
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.LdcReplacement));
        CompiledAuthoringSelectedInput[] inputs =
        [
            .. slotPaths.Select(pair =>
            {
                string addressSpaceId = pair.Key switch
                {
                    CompositionSlotIds.ReplaceBase => CompositionAddressSpaceIds.ReferenceBase,
                    CompositionSlotIds.ReplaceLdc => CompositionAddressSpaceIds.LdcReplacement,
                    _ => replacementBinding.AddressSpaceId,
                };
                return new CompiledAuthoringSelectedInput(
                    addressSpaceId,
                    pair.Value,
                    File.ReadAllBytes(pair.Value));
            }),
        ];
        var session = new AuthoringSessionState(ExperienceIds.DpReplace);
        return Services.DpReplaceAuthoring.PrepareSession(
                session,
                icId,
                inputs);
    }

    private static IReadOnlyList<CompositionIssue> PreparationIssues(
        CompiledAuthoringSessionPreparation prepared)
    {
        InputSelectionMemberReadiness[] readiness =
        [
            .. prepared.Selection.Slots.Where(static member => member.IssueCode is not null),
        ];
        return readiness.Length != 0
            ?
            [
                .. readiness.Select(static member => new CompositionIssue(
                    member.IssueCode!,
                    member.Reason ?? "DP Replace selection is not ready.")),
            ]
            : prepared.Issues.Count != 0
                ? prepared.Issues
                : [new CompositionIssue(
                    prepared.SessionIssue?.Code ??
                        InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                    prepared.SessionIssue?.Message ??
                        "DP Replace preparation did not produce one accepted session.")];
    }
}
