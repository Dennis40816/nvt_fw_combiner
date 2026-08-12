using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class DpReplaceTestSupport
{
    internal static async ValueTask<CompositionRunResult> RunAsync(
        string icId,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        CompositionRunProgressFeed? progress = null)
    {
        var host = CompositionHostServices.Create();
        if (!StringComparer.OrdinalIgnoreCase.Equals(replaceMode, ExperienceIds.DpReplace))
        {
            throw new ArgumentException("DP test support accepts only DP Replace.", nameof(replaceMode));
        }

        host.WarmCanonicalCapabilities(cancellationToken);
        if (!host.CompositionCapabilityExperience
                .IsReplaceWorkflowAvailable(icId, ExperienceIds.DpReplace) ||
            !slotPaths.ContainsKey(CompositionSlotIds.ReplaceBase))
        {
            throw new InvalidOperationException(
                $"No supported V2 DP Replace profile is registered for {icId}.");
        }

        CompiledAuthoringSelectionSnapshot discovery =
            host.DpReplaceAuthoring.GetAuthoringSnapshot(
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
        CompiledAuthoringSelectedInput[] selectedInputs =
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
        CompiledAuthoringSessionPreparation prepared =
            host.DpReplaceAuthoring.PrepareSession(
                session,
                icId,
                selectedInputs);
        ActiveSessionSnapshot snapshot = prepared.Succeeded
            ? prepared.Snapshot!
            : throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                PreparationIssues(prepared).Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));

        return await host.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    snapshot,
                    slotPaths,
                    build,
                    outputPath: outputPath),
                progress ?? new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
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
