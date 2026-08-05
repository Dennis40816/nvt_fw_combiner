using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionExecutionAdapter
{
    /// <summary>
    /// Ephemeral CLI/Saved Rule boundary: inspect once, then execute the exact
    /// content-bound draft through the strict General Merge runner.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeEphemeralDraftAsync(
        string icId,
        GeneralMergeDraftState draft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return RunGeneralMergeEphemeralDraftAsync(
            icId,
            draft,
            savedRulePolicy: null,
            build,
            cancellationToken,
            outputPath);
    }

    /// <summary>
    /// Runs a Saved Rule draft with its separate resource-narrowing authority.
    /// </summary>
    internal static ValueTask<WorkbenchRunResult> RunGeneralMergeEphemeralDraftAsync(
        string icId,
        GeneralMergeDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return RunGeneralMergeWithInitialInspectionAsync(
            icId,
            draft,
            draftIssues: null,
            savedRulePolicy,
            new AuthoringRevision(1),
            build,
            outputPath,
            progress: null,
            cancellationToken);
    }

    /// <summary>
    /// Runs General Merge from the exact content-bound draft returned by an
    /// earlier desktop Preview or explicit Reload/Rebind.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeAcceptedSessionWithProgressAsync(
        string icId,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ResolvedCapability capability = AcceptedAuthoringSessionBinding.RequireCapability(
            acceptedSession,
            Profiles.IcWorkflowIds.GeneralMerge,
            icId,
            AuthoringDerivedResultKind.Validation);
        GeneralMergeDraftState draft = acceptedSession.DraftState as GeneralMergeDraftState ??
            throw new InvalidOperationException(
                "The accepted General Merge session has no exact typed draft.");
        return RunGeneralMergeV2Async(
            icId,
            draft,
            draftIssues: null,
            savedRulePolicy: null,
            build,
            cancellationToken,
            outputPath,
            progress,
            capability);
    }

    private static async ValueTask<WorkbenchRunResult>
        RunGeneralMergeWithInitialInspectionAsync(
            string icId,
            GeneralMergeDraftState? draft,
            IReadOnlyList<CompositionIssue>? draftIssues,
            GeneralSavedRuleResourcePolicy? savedRulePolicy,
            AuthoringRevision inspectionRevision,
            bool build,
            string? outputPath,
            CompositionRunProgressFeed? progress,
            CancellationToken cancellationToken)
    {
        if (draft is not null &&
            draftIssues is not { Count: > 0 })
        {
            GeneralSelectedFileBindingResult accepted =
                await CanonicalAuthoringAdapter.InspectGeneralSelectedFilesAsync(
                    draft.Mappings,
                    inspectionRevision,
                    cancellationToken)
                .ConfigureAwait(false);
            if (accepted.Succeeded)
            {
                draft = new GeneralMergeDraftState(
                    draft.OutputInitializer,
                    accepted.Draft!);
            }
            else
            {
                draftIssues = accepted.Issues;
            }
        }

        return await RunGeneralMergeV2Async(
            icId,
            draft,
            draftIssues,
            savedRulePolicy,
            build,
            cancellationToken,
            outputPath,
            progress).ConfigureAwait(false);
    }
}
